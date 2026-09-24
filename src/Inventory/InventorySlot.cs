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

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Live Il2Cpp/Unity interop against game assemblies; needs running game.")]
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

                // ALL items kept disabled in stash: invisible,
                // no Update()/raycast, no save scans. CableSpinners also
                // teleported to stash position (backup for
                // Y harmony patch); RestoreToHand reactivates them.
                if (go.GetComponent<CableSpinner>() != null)
                {
                    go.transform.SetParent(null, false);
                    go.transform.position = StashPosition;
                    go.SetActive(false);
                }
                else
                {
                    go.transform.SetParent(null, false);
                    go.SetActive(false);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Live Il2Cpp/Unity interop against game assemblies; needs running game.")]
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

                    // NO more InteractOnClick(): that is the vanilla
                    // click-pickup handler. Called programmatically it
                    // re-created vanilla UI elements/hand copies per restore
                    // (UI duplicates + item duplication per slot switch).
                    // Instead hand state is set directly; all
                    // fields vanilla sets on pickup (parent, transform,
                    // objectInHands, PlayerManager hand array)
                    // RestoreSlotItems/RestoreToHand maintain manually.
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
