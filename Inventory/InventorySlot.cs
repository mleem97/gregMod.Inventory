using Il2Cpp;
using UnityEngine;

namespace GregModInventory
{
    public class InventorySlot
    {
        public PlayerManager.ObjectInHand ItemType { get; }
        public GameObject[] StoredObjects { get; }

        public string DisplayName { get; }
        public int PrefabID { get; }

        public Texture2D Icon { get; }

        public Vector3[] SavedLocalPositions { get; }
        public Quaternion[] SavedLocalRotations { get; }

        // Stash high above the world — avoids negative-Y kill zones some games use.
        // Must stay above StashThreshold used in the Harmony patch (1000f).
        private static readonly Vector3 StashPosition = new Vector3(0, 5000, 0);

        public InventorySlot(PlayerManager.ObjectInHand itemType, GameObject[] objects,
                             string displayName, int prefabID, Texture2D icon = null)
        {
            ItemType = itemType;
            StoredObjects = objects;
            DisplayName = displayName;
            PrefabID = prefabID;
            Icon = icon;

            SavedLocalPositions = new Vector3[objects.Length];
            SavedLocalRotations = new Quaternion[objects.Length];

            for (int i = 0; i < objects.Length; i++)
            {
                var go = objects[i];
                if (go == null) continue;
                SavedLocalPositions[i] = go.transform.localPosition;
                SavedLocalRotations[i] = go.transform.localRotation;
            }
        }

        public void Stash()
        {
            foreach (var go in StoredObjects)
            {
                if (go == null) continue;

                var usable = go.GetComponent<UsableObject>()
                          ?? go.GetComponent<CableSpinner>()?.TryCast<UsableObject>();
                if (usable != null)
                {
                    usable.objectInHands = false;
                    if (usable.rb != null)
                    {
                        usable.rb.isKinematic = true;
                        usable.rb.velocity = Vector3.zero;
                        usable.rb.angularVelocity = Vector3.zero;
                    }

                    if (usable.inputctrl != null)
                        Core.CachedInputCtrl = usable.inputctrl;
                }

                // CableSpinners are teleported so the Harmony patch can detect them by Y position.
                // All other items are deactivated in place — this stops any game Update() logic
                // (e.g. QSFP port cleanup) that would destroy sub-components when the object
                // is at an invalid world position.
                if (go.GetComponent<CableSpinner>() != null)
                {
                    go.transform.SetParent(null, false);
                    go.transform.position = StashPosition;
                }
                else
                {
                    go.transform.SetParent(null, false);
                    go.SetActive(false);
                }
            }
        }

        public void RestoreToHand(Transform handParent)
        {
            for (int i = 0; i < StoredObjects.Length; i++)
            {
                var go = StoredObjects[i];
                if (go == null) continue;

                if (!go.activeSelf)
                    go.SetActive(true);

                go.transform.SetParent(handParent, false);
                go.transform.localPosition = SavedLocalPositions[i];
                go.transform.localRotation = SavedLocalRotations[i];

                var usable = go.GetComponent<UsableObject>()
                          ?? go.GetComponent<CableSpinner>()?.TryCast<UsableObject>();
                if (usable != null)
                {
                    if (usable.rb != null)
                    {
                        usable.rb.isKinematic = true;
                        usable.rb.velocity = Vector3.zero;
                        usable.rb.angularVelocity = Vector3.zero;
                    }

                        // Call InteractOnClick to re-initialize any game-internal state set
                    // during pickup (e.g. cable placement color). The Harmony patch allows
                    // this since the object is at hand height (y < StashThreshold).
                    usable.objectInHands = false;
                    usable.InteractOnClick();

                    // Re-apply our saved transform — InteractOnClick may have repositioned it
                    go.transform.SetParent(handParent, false);
                    go.transform.localPosition = SavedLocalPositions[i];
                    go.transform.localRotation = SavedLocalRotations[i];

                    // Force objectInHands true in case InteractOnClick didn't set it
                    usable.objectInHands = true;
                }
            }
        }

        public bool IsEmpty()
        {
            foreach (var go in StoredObjects)
                if (go != null) return false;
            return true;
        }

        public int AliveCount()
        {
            int count = 0;
            foreach (var go in StoredObjects)
                if (go != null) count++;
            return count;
        }
    }
}
