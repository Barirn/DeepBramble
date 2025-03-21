﻿using UnityEngine;

namespace DeepBramble.BaseInheritors
{
    class GravCrystalSocket : OWItemSocket
    {
        private DirectionalForceVolume[] gravFields = null;
        private ParticleSystem[] gravParticles = null;

        /**
         * Need to give the transform before the base awake method (it'll always be the active transform)
         */
        public override void Awake()
        {
            _socketTransform = transform;

            base.Awake();
        }

        /**
         * Modified start, need to also grab grav field
         */
        public override void Start()
        {
            base.Start();

            gravFields = GetComponentsInChildren<DirectionalForceVolume>();
            gravParticles = GetComponentsInChildren<ParticleSystem>();
            if (gravFields.Length == 0)
                DeepBramble.debugPrint("Grav Crystal Socket failed to find gravity field!");

            //Need to disable it if we have no crystal
            if (_socketedItem == null)
            {
                foreach (DirectionalForceVolume field in gravFields)
                    field.gameObject.SetActive(false);
                foreach (ParticleSystem particleSystem in gravParticles)
                    particleSystem.Stop();
            }

            //Otherwise, enable it
            else
            {
                foreach (DirectionalForceVolume field in gravFields)
                    field.gameObject.SetActive(true);
                foreach (ParticleSystem particleSystem in gravParticles)
                    particleSystem.Play();
            }
        }

        /**
         * Only accepts items that are gravity crystal items
         * 
         * @param item The item to be checked
         * @return True if it is acceptable, false otherwise
         */
        public override bool AcceptsItem(OWItem item)
        {
            return item is GravCrystalItem;
        }

        /**
         * When something gets slotted in, need to enable the gravity field
         * 
         * @param item The item that was placed
         */
        public override bool PlaceIntoSocket(OWItem item)
        {
            bool ret = base.PlaceIntoSocket(item);
            if (ret && (item as GravCrystalItem).intact)
            {
                foreach (DirectionalForceVolume field in gravFields)
                    field.gameObject.SetActive(true);
                foreach (ParticleSystem particleSystem in gravParticles)
                    particleSystem.Play();
            }
            return ret;
        }

        /**
         * When something is removed, disable the field
         */
        public override OWItem RemoveFromSocket()
        {
            OWItem ret = base.RemoveFromSocket();
            if (ret)
            {
                foreach (DirectionalForceVolume field in gravFields)
                    field.gameObject.SetActive(false);
                foreach (ParticleSystem particleSystem in gravParticles)
                {
                    particleSystem.Stop();
                    particleSystem.Clear();
                }
            }
            return ret;
        }
    }
}
