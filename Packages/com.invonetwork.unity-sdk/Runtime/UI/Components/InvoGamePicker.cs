using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    /// <summary>
    /// Plaid-style destination picker: search pinned at the top, the games the player already plays
    /// as tiles, every other game as a list that filters as they type, unavailable games greyed with a
    /// reason, and a clear no-results state. The send and transfer panels fill it and listen to
    /// <see cref="Picked"/>; it works with any layout that wires these fields.
    /// </summary>
    public class InvoGamePicker : MonoBehaviour
    {
        [Tooltip("Shown while picking. Defaults to this GameObject.")]
        public GameObject root;
        public TMP_InputField searchInput;
        public Button closeButton;

        [Header("Games you play")]
        public GameObject yourGamesSection;
        public Transform yourGamesContainer;
        public InvoGameEntryView tileTemplate;
        [Min(1)] public int maxYourGames = 6;

        [Header("Every other game")]
        public TMP_Text listHeader;
        public Transform listContainer;
        public InvoGameEntryView rowTemplate;
        [Tooltip("Rows shown before the player searches. Searching shows every match.")]
        [Min(1)] public int maxRowsBeforeSearch = 8;
        public TMP_Text footerText;
        public TMP_Text emptyText;

        /// <summary>Raised when the player picks an available game. The picker closes itself.</summary>
        public event Action<InvoGameOption> Picked;

        private readonly List<InvoGameOption> options = new List<InvoGameOption>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private string selectedId;

        private GameObject Root => root != null ? root : gameObject;

        private void Awake()
        {
            if (tileTemplate != null) tileTemplate.gameObject.SetActive(false);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (searchInput != null) searchInput.onValueChanged.AddListener(_ => Refresh());
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void SetOptions(IEnumerable<InvoGameOption> games, string selectedGameId)
        {
            options.Clear();
            if (games != null)
                options.AddRange(games);
            selectedId = selectedGameId;
            if (Root.activeInHierarchy)
                Refresh();
        }

        public void Open()
        {
            Root.SetActive(true);
            Root.transform.SetAsLastSibling();
            if (searchInput != null)
            {
                searchInput.text = string.Empty;
                searchInput.ActivateInputField();
            }
            Refresh();
        }

        public void Close() => Root.SetActive(false);

        public void Refresh()
        {
            foreach (GameObject go in spawned)
                if (go != null) Destroy(go);
            spawned.Clear();

            string query = searchInput != null ? searchInput.text : string.Empty;
            bool searching = !string.IsNullOrWhiteSpace(query);

            int tiles = 0;
            if (!searching && yourGamesContainer != null && tileTemplate != null)
            {
                foreach (InvoGameOption option in InvoGameSearch.Filter(options, null))
                {
                    if (!option.isYours || tiles >= maxYourGames)
                        continue;
                    Spawn(tileTemplate, yourGamesContainer, option);
                    tiles++;
                }
            }
            if (yourGamesSection != null) yourGamesSection.SetActive(tiles > 0);

            List<InvoGameOption> rows = InvoGameSearch.Filter(options, query);
            if (!searching && tiles > 0)
                rows.RemoveAll(o => o.isYours);
            int shown = 0;
            foreach (InvoGameOption option in rows)
            {
                if (!searching && shown >= maxRowsBeforeSearch)
                    break;
                Spawn(rowTemplate, listContainer, option);
                shown++;
            }

            if (listHeader != null)
            {
                listHeader.text = searching ? "Results" : (tiles > 0 ? "More games on Invo" : "Games on Invo");
                listHeader.gameObject.SetActive(shown > 0);
            }
            if (emptyText != null)
            {
                emptyText.text = $"No games match \"{query.Trim()}\". Check the spelling, or try the studio or platform.";
                emptyText.gameObject.SetActive(searching && shown == 0);
            }
            if (footerText != null)
            {
                int hidden = rows.Count - shown;
                footerText.text = hidden > 0
                    ? $"{hidden} more. Search above to find your game."
                    : "Don't see your game? Search above by name, studio or platform.";
                footerText.gameObject.SetActive(!searching);
            }
            if (searchInput != null && searchInput.placeholder is TMP_Text placeholder)
                placeholder.text = $"Search {options.Count} games";
        }

        private void Spawn(InvoGameEntryView template, Transform parent, InvoGameOption option)
        {
            if (template == null || parent == null)
                return;
            InvoGameEntryView view = Instantiate(template, parent);
            view.gameObject.SetActive(true);
            view.Bind(option, option.id == selectedId, OnPicked);
            spawned.Add(view.gameObject);
        }

        private void OnPicked(InvoGameOption option)
        {
            if (option == null || !option.available)
                return;
            selectedId = option.id;
            Close();
            Picked?.Invoke(option);
        }
    }
}
