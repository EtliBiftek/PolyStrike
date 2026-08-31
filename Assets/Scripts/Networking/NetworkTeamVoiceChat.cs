using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkTeamVoiceChat : MonoBehaviour
    {
        private readonly Dictionary<string, VivoxParticipant> participants = new Dictionary<string, VivoxParticipant>();

        private bool operationInFlight;
        private bool serviceReady;
        private bool channelReady;
        private bool transmitting;
        private bool transmissionInFlight;
        private bool participantEventsBound;
        private string currentChannel = string.Empty;
        private string statusKey = "voice.status.starting";

        private void Update()
        {
            if (!TryGetLocalPlayer(out var localState) || localState.VoiceRoom.IsEmpty)
                return;

            var wantedChannel = BuildChannelName(localState.VoiceRoom, localState.Team);
            if ((!channelReady || !string.Equals(wantedChannel, currentChannel, StringComparison.Ordinal)) && !operationInFlight)
                _ = EnsureChannelAsync(wantedChannel);

            if (!channelReady || transmissionInFlight)
                return;

            var wantsToTalk = GameInput.VoiceHeld;
            if (wantsToTalk != transmitting)
                _ = SetTransmissionAsync(wantsToTalk);
        }

        private async Task EnsureChannelAsync(string wantedChannel)
        {
            operationInFlight = true;
            channelReady = false;
            statusKey = "voice.status.starting";

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                if (!serviceReady)
                {
                    await VivoxService.Instance.InitializeAsync();
                    BindParticipantEvents();
                    await VivoxService.Instance.LoginAsync(new LoginOptions
                    {
                        DisplayName = NetworkConnectionMenu.LocalPlayerName,
                        DisableAutomaticChannelTransmissionSwap = true
                    });
                    await VivoxService.Instance.EnableAutoVoiceActivityDetectionAsync();
                    serviceReady = true;
                }

                if (!string.IsNullOrEmpty(currentChannel) &&
                    !string.Equals(currentChannel, wantedChannel, StringComparison.Ordinal))
                {
                    await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
                    await VivoxService.Instance.LeaveChannelAsync(currentChannel);
                    participants.Clear();
                    transmitting = false;
                }

                if (!string.Equals(currentChannel, wantedChannel, StringComparison.Ordinal))
                    await VivoxService.Instance.JoinGroupChannelAsync(wantedChannel, ChatCapability.AudioOnly);

                currentChannel = wantedChannel;
                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
                transmitting = false;
                channelReady = true;
                statusKey = "voice.status.ready";
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PolyStrike team voice unavailable: {exception.Message}");
                currentChannel = string.Empty;
                channelReady = false;
                statusKey = "voice.status.unavailable";
            }
            finally
            {
                operationInFlight = false;
            }
        }

        private async Task SetTransmissionAsync(bool enabled)
        {
            transmissionInFlight = true;
            try
            {
                if (enabled)
                {
                    await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, currentChannel);
                    transmitting = true;
                }
                else
                {
                    await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
                    transmitting = false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"PolyStrike push to talk failed: {exception.Message}");
                transmitting = false;
                channelReady = false;
                statusKey = "voice.status.unavailable";
            }
            finally
            {
                transmissionInFlight = false;
            }
        }

        private void BindParticipantEvents()
        {
            if (participantEventsBound)
                return;

            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
            participantEventsBound = true;
        }

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            if (participant == null)
                return;
            participants[participant.PlayerId] = participant;
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            if (participant == null)
                return;
            participants.Remove(participant.PlayerId);
        }

        private void OnGUI()
        {
            if (!TryGetLocalPlayer(out _))
                return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;

            var y = Screen.height - 176f;
            if (transmitting)
            {
                GUI.Box(new Rect(18f, y, 260f, 34f), string.Empty);
                GUI.Label(new Rect(30f, y + 4f, 236f, 26f), Localization.Get("voice.transmitting"), style);
                y -= 38f;
            }
            else if (GameInput.VoiceHeld && !channelReady)
            {
                GUI.Box(new Rect(18f, y, 300f, 34f), string.Empty);
                GUI.Label(new Rect(30f, y + 4f, 276f, 26f), Localization.Get(statusKey), style);
                y -= 38f;
            }

            foreach (var pair in participants)
            {
                var participant = pair.Value;
                if (participant == null || participant.IsSelf || !participant.SpeechDetected ||
                    !string.Equals(participant.ChannelName, currentChannel, StringComparison.Ordinal))
                    continue;

                var text = Localization.Get("voice.speaking").Replace("{0}", participant.DisplayName);
                GUI.Box(new Rect(18f, y, 260f, 32f), string.Empty);
                GUI.Label(new Rect(30f, y + 3f, 236f, 26f), text, style);
                y -= 35f;
            }
        }

        private static string BuildChannelName(FixedString64Bytes room, byte team)
        {
            return $"{room}_{(team == 0 ? "t" : "ct")}";
        }

        private static bool TryGetLocalPlayer(out NetworkPlayerState playerState)
        {
            playerState = default;
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkPlayerState>());
            var entities = query.ToEntityArray(Allocator.Temp);
            var found = false;

            for (var i = 0; i < entities.Length; i++)
            {
                if (!entityManager.HasComponent<GhostOwnerIsLocal>(entities[i]))
                    continue;

                playerState = entityManager.GetComponentData<NetworkPlayerState>(entities[i]);
                found = true;
                break;
            }

            entities.Dispose();
            query.Dispose();
            return found;
        }

        private async void OnDestroy()
        {
            try
            {
                if (participantEventsBound)
                {
                    VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
                    VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
                    participantEventsBound = false;
                }

                if (!serviceReady)
                    return;

                await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
                await VivoxService.Instance.LeaveAllChannelsAsync();
                await VivoxService.Instance.LogoutAsync();
            }
            catch
            {
            }
        }
    }
}
