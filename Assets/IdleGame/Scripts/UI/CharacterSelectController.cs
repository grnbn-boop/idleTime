using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IdleTime.Core;
using IdleTime.Interactions;

namespace IdleTime.UI
{
    // Blocked-in roster screen modelled on the reference mock: a horizontal row of
    // character "cards" (flat-colour cube portraits for now). Clicking a card makes that
    // character active and drops into the first level (testBed until level one exists).
    //
    // On a fresh save with no account yet, an overlay first asks for an account name,
    // mirroring the reference flow ("if there is no account data, you enter your name").
    //
    // The roster is driven by PlayerManager.Characters (authored on the GameSystems
    // prefab and loaded from saves before this scene runs). Slots beyond the authored
    // characters render as dim, non-interactive "Empty" placeholders so the row reads as
    // a fixed-size roster even while only one character exists.
    public class CharacterSelectController : MonoBehaviour
    {
        [SerializeField] string firstLevelScene = "testBed";
        [SerializeField] int rosterSlots = 6;

        Canvas canvas;
        RectTransform row;

        void Start()
        {
            canvas = MenuUIBuilder.CreateCanvas("CharacterSelectCanvas", transform);

            var bg = MenuUIBuilder.CreateImage(canvas.transform, "Background", new Color(0.10f, 0.13f, 0.12f));
            MenuUIBuilder.Stretch(bg.rectTransform);

            var heading = MenuUIBuilder.CreateText(canvas.transform, "SELECT CHARACTER", 70, FontStyles.Bold);
            var hr = heading.rectTransform;
            hr.anchorMin = new Vector2(0, 1);
            hr.anchorMax = new Vector2(1, 1);
            hr.pivot = new Vector2(0.5f, 1);
            hr.anchoredPosition = new Vector2(0, -70);
            hr.sizeDelta = new Vector2(0, 100);

            BuildRosterRow();
            BuildRoster();
            MaybePromptAccount();
        }

        void BuildRosterRow()
        {
            var rowGO = new GameObject("Roster", typeof(RectTransform));
            rowGO.transform.SetParent(canvas.transform, false);
            row = (RectTransform)rowGO.transform;
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(1760, 420);

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28;
            layout.childAlignment = TextAnchor.MiddleCenter;
            // childControl* lets each card's LayoutElement.preferred size drive its width
            // /height; forceExpand off keeps them at that fixed card size instead of
            // stretching to fill the row.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        void BuildRoster()
        {
            var pm = PlayerManager.Instance;
            var characters = pm != null ? pm.Characters : null;
            int count = characters != null ? characters.Count : 0;
            int slots = Mathf.Max(rosterSlots, count);

            for (int i = 0; i < slots; i++)
            {
                if (i < count) BuildCharacterCard(i, characters[i]);
                else BuildEmptyCard();
            }
        }

        void BuildCharacterCard(int index, CharacterData c)
        {
            var card = MakeCard(MenuUIBuilder.Panel, interactable: true);

            var portrait = MenuUIBuilder.CreateImage(card.transform, "Portrait", PortraitColor(index));
            MenuUIBuilder.Place(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(150, 150));
            portrait.rectTransform.pivot = new Vector2(0.5f, 1f);

            var name = MenuUIBuilder.CreateText(card.transform, c.characterName, 30, FontStyles.Bold);
            BottomText(name.rectTransform, 78, 40);

            var info = MenuUIBuilder.CreateText(card.transform, $"Lv {c.level}  {c.ClassName}", 22);
            info.color = new Color(1f, 1f, 1f, 0.65f);
            BottomText(info.rectTransform, 38, 32);

            card.GetComponent<Button>().onClick.AddListener(() => Play(index));
        }

        void BuildEmptyCard()
        {
            var card = MakeCard(new Color(1f, 1f, 1f, 0.04f), interactable: false);
            var label = MenuUIBuilder.CreateText(card.transform, "Empty", 26, FontStyles.Italic);
            label.color = new Color(1f, 1f, 1f, 0.25f);
            MenuUIBuilder.Stretch(label.rectTransform);
        }

        // A fixed-size card root: LayoutElement keeps the HorizontalLayoutGroup from
        // resizing it, an Image is the background, and a Button drives selection.
        GameObject MakeCard(Color background, bool interactable)
        {
            var go = new GameObject("Card", typeof(RectTransform));
            go.transform.SetParent(row, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 240;
            le.preferredHeight = 380;

            var img = go.AddComponent<Image>();
            img.color = background;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = interactable;
            return go;
        }

        // Stable, distinct placeholder portrait colour per slot until real art exists.
        static Color PortraitColor(int index) =>
            Color.HSVToRGB((index * 0.137f) % 1f, 0.45f, 0.85f);

        // Anchor a text element to the bottom of its card, full width, at the given height.
        static void BottomText(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(0, height);
        }

        void Play(int index)
        {
            PlayerManager.Instance?.SwitchCharacter(index);
            LevelLoader.Go(firstLevelScene);
        }

        // ── Account-name gate ──────────────────────────────────────────────────────

        void MaybePromptAccount()
        {
            var save = SaveManager.Instance;
            if (save == null || save.HasAccount) return;
            ShowAccountPrompt(save);
        }

        void ShowAccountPrompt(SaveManager save)
        {
            var overlay = MenuUIBuilder.CreateImage(canvas.transform, "AccountOverlay", new Color(0, 0, 0, 0.78f));
            MenuUIBuilder.Stretch(overlay.rectTransform);   // raycast target blocks the roster behind it

            var panel = MenuUIBuilder.CreateImage(overlay.transform, "Panel", new Color(0.14f, 0.17f, 0.22f));
            MenuUIBuilder.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 380));

            var title = MenuUIBuilder.CreateText(panel.transform, "Create Account", 44, FontStyles.Bold);
            MenuUIBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.84f), Vector2.zero, new Vector2(640, 60));

            var prompt = MenuUIBuilder.CreateText(panel.transform, "Enter your account name", 26);
            prompt.color = new Color(1f, 1f, 1f, 0.7f);
            MenuUIBuilder.Place(prompt.rectTransform, new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(640, 40));

            var input = MenuUIBuilder.CreateInputField(panel.transform, "Account name", new Vector2(540, 66));
            MenuUIBuilder.Place((RectTransform)input.transform, new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(540, 66));

            var confirm = MenuUIBuilder.CreateButton(panel.transform, "CONFIRM", new Vector2(300, 76),
                MenuUIBuilder.Accent, null);
            MenuUIBuilder.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0.18f), Vector2.zero, new Vector2(300, 76));

            confirm.onClick.AddListener(() =>
            {
                if (string.IsNullOrWhiteSpace(input.text)) return;
                save.SetAccountName(input.text);
                Destroy(overlay.gameObject);
            });

            input.Select();
        }
    }
}
