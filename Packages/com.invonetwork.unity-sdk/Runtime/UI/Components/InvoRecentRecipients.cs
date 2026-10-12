using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// A row of the numbers this player sent to before (see <see cref="InvoRecentRecipientsStore"/>).
    /// Tapping one fills the recipient field. Hidden when there are none.
    /// </summary>
    public class InvoRecentRecipients : MonoBehaviour
    {
        [Tooltip("Hidden when there are no recent recipients. Defaults to this GameObject.")]
        public GameObject section;
        public Transform container;
        public InvoRecipientChip chipTemplate;

        /// <summary>Raised with the E.164 number the player tapped.</summary>
        public event Action<string> Picked;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private string selected;

        private void Awake()
        {
            if (chipTemplate != null) chipTemplate.gameObject.SetActive(false);
        }

        public void SetRecipients(IList<string> phones)
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();

            int count = 0;
            if (phones != null && chipTemplate != null && container != null)
            {
                foreach (string phone in phones)
                {
                    InvoRecipientChip chip = Instantiate(chipTemplate, container);
                    chip.gameObject.SetActive(true);
                    chip.Bind(phone, phone == selected, OnPicked);
                    spawned.Add(chip.gameObject);
                    count++;
                }
                chipTemplate.transform.SetAsLastSibling();
            }
            (section != null ? section : gameObject).SetActive(count > 0);
        }

        /// <summary>Highlights the chip for <paramref name="phone"/>, if any (e.g. after the player typed it).</summary>
        public void Select(string phone)
        {
            selected = phone;
            foreach (GameObject go in spawned)
            {
                var chip = go != null ? go.GetComponent<InvoRecipientChip>() : null;
                if (chip != null) chip.SetSelected(chip.Phone == phone);
            }
        }

        private void OnPicked(string phone)
        {
            Select(phone);
            Picked?.Invoke(phone);
        }
    }
}
