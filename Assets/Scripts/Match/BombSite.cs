using System.Collections.Generic;
using UnityEngine;

namespace PolyStrike.Match
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BombSite : MonoBehaviour
    {
        private static readonly List<BombSite> Sites = new List<BombSite>();
        private BoxCollider volume;

        public string SiteId { get; private set; } = "A";
        public Vector3 PlantPosition => volume != null ? volume.bounds.center : transform.position;

        private void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
        }

        private void OnEnable()
        {
            if (!Sites.Contains(this))
                Sites.Add(this);
        }

        private void OnDisable()
        {
            Sites.Remove(this);
        }

        public void Configure(string siteId)
        {
            SiteId = string.IsNullOrWhiteSpace(siteId) ? "A" : siteId;
        }

        public bool Contains(Vector3 point)
        {
            return volume != null && volume.bounds.Contains(point);
        }

        public static BombSite FindAt(Vector3 point)
        {
            for (var i = 0; i < Sites.Count; i++)
            {
                var site = Sites[i];
                if (site != null && site.Contains(point))
                    return site;
            }

            return null;
        }
    }
}
