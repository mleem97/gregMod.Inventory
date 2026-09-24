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

                // ALLE Items werden deaktiviert im Stash gehalten: unsichtbar,
                // kein Update()/Raycast, keine Save-Scans. CableSpinner werden
                // zusaetzlich auf die Stash-Position teleportiert (Backup fuer
                // den Y-Harmony-Patch); RestoreToHand reaktiviert sie wieder.
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

                    // KEIN InteractOnClick() mehr: Das ist der Vanilla
                    // Click-Pickup-Handler. Programmatisch aufgerufen hat er
                    // pro Restore Vanilla-UI-Elemente/hand-Kopien nacherzeugt
                    // (UI-Duplikate + Item-Vermehrung pro Slot-Wechsel).
                    // Stattdessen wird der Hand-Status direkt gesetzt; alle
                    // Felder, die Vanilla beim Pickup setzt (Parent, Transform,
                    // objectInHands, PlayerManager-Hand-Array), pflegt
                    // RestoreSlotItems/RestoreToHand manuell.
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
