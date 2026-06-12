using UnityEngine;
using TMPro;
using IdleTime.Interactions;

namespace IdleTime.UI
{
    // The game's blocked-in front door: shows the title and a START button that takes
    // the player to character select. Self-builds its canvas (see MenuUIBuilder), so the
    // scene only needs this one component on an otherwise empty GameObject plus an
    // EventSystem. Art is a flat-colour background "cube" until real assets land.
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] string gameTitle = "IDLE TIME";
        [SerializeField] string subtitle = "Idle RPG Prototype";
        [SerializeField] string characterSelectScene = "character_select";

        void Start() => Build();

        void Build()
        {
            var canvas = MenuUIBuilder.CreateCanvas("TitleCanvas", transform);

            var bg = MenuUIBuilder.CreateImage(canvas.transform, "Background", new Color(0.09f, 0.11f, 0.16f));
            MenuUIBuilder.Stretch(bg.rectTransform);

            var title = MenuUIBuilder.CreateText(canvas.transform, gameTitle, 140, FontStyles.Bold);
            MenuUIBuilder.Place(title.rectTransform, new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(1500, 240));

            var sub = MenuUIBuilder.CreateText(canvas.transform, subtitle, 40);
            sub.color = new Color(1f, 1f, 1f, 0.6f);
            MenuUIBuilder.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 80));

            var start = MenuUIBuilder.CreateButton(canvas.transform, "START", new Vector2(420, 110),
                MenuUIBuilder.Accent, OnStart);
            MenuUIBuilder.Place((RectTransform)start.transform, new Vector2(0.5f, 0.28f), Vector2.zero, new Vector2(420, 110));
        }

        void OnStart() => LevelLoader.Go(characterSelectScene);
    }
}
