using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using IdleTime.Player;

namespace IdleTime.Interactions
{
    [DefaultExecutionOrder(-2)]
    public class NpcDialogue : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] protected TextMeshPro dialogueText;
        [TextArea(2, 5)]
        [SerializeField] protected string[] lines;
        [SerializeField] private float charactersPerSecond = 45f;
        [SerializeField] private int textSortingOrder = 30;
        [SerializeField] private bool loopDialogue;
        [SerializeField] private bool hideWhenFinished = true;

        [Header("Click Target")]
        [SerializeField] private Collider2D clickCollider;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool suppressPlayerMoveOnClick = true;

        private Coroutine typeRoutine;
        protected int lineIndex = -1;
        private int visibleCharacterCount;
        private ClickToMove2D playerMovement;

        protected bool IsTyping => typeRoutine != null;
        protected bool HasVisibleText => dialogueText != null && dialogueText.gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (dialogueText == null)
                dialogueText = GetComponentInChildren<TextMeshPro>(true);
            if (clickCollider == null)
                clickCollider = GetComponent<Collider2D>();
            if (worldCamera == null)
                worldCamera = Camera.main;

            playerMovement = FindAnyObjectByType<ClickToMove2D>();

            if (dialogueText != null)
            {
                Renderer textRenderer = dialogueText.GetComponent<Renderer>();
                if (textRenderer != null)
                    textRenderer.sortingOrder = textSortingOrder;

                dialogueText.text = string.Empty;
                dialogueText.maxVisibleCharacters = 0;
                dialogueText.gameObject.SetActive(false);
            }
        }

        protected virtual void Update()
        {
            if (!WasClickedThisFrame())
                return;

            if (suppressPlayerMoveOnClick)
            {
                if (playerMovement == null)
                    playerMovement = FindAnyObjectByType<ClickToMove2D>();
                playerMovement?.SuppressNextClick();
            }

            Interact();
        }

        public virtual void Interact()
        {
            if (IsTyping)
            {
                CompleteCurrentLine();
                return;
            }

            if (lines == null || lines.Length == 0)
                return;

            lineIndex++;
            if (lineIndex >= lines.Length)
            {
                if (!loopDialogue)
                {
                    lineIndex = -1;
                    OnDialogueFinished();
                    if (hideWhenFinished)
                        HideText();
                    return;
                }

                lineIndex = 0;
            }

            Say(lines[lineIndex]);
        }

        protected void Say(string message)
        {
            if (dialogueText == null)
                return;

            if (typeRoutine != null)
                StopCoroutine(typeRoutine);

            dialogueText.gameObject.SetActive(true);
            dialogueText.text = message;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();
            visibleCharacterCount = dialogueText.textInfo.characterCount;
            typeRoutine = StartCoroutine(TypeLine());
        }

        protected void CompleteCurrentLine()
        {
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
                typeRoutine = null;
            }

            if (dialogueText != null)
                dialogueText.maxVisibleCharacters = visibleCharacterCount;
        }

        protected void HideText()
        {
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
                typeRoutine = null;
            }

            if (dialogueText == null)
                return;

            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.gameObject.SetActive(false);
        }

        protected virtual void OnDialogueFinished()
        {
        }

        private IEnumerator TypeLine()
        {
            float visibleCharacters = 0f;
            float speed = Mathf.Max(1f, charactersPerSecond);

            while (dialogueText != null && dialogueText.maxVisibleCharacters < visibleCharacterCount)
            {
                visibleCharacters += speed * Time.deltaTime;
                dialogueText.maxVisibleCharacters = Mathf.Min(visibleCharacterCount, Mathf.FloorToInt(visibleCharacters));
                yield return null;
            }

            if (dialogueText != null)
                dialogueText.maxVisibleCharacters = visibleCharacterCount;
            typeRoutine = null;
        }

        protected bool WasClickedThisFrame()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
                return false;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
            if (cameraToUse == null)
                return false;

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector2 worldPosition = cameraToUse.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));

            if (clickCollider != null)
                return clickCollider.OverlapPoint(worldPosition);

            foreach (var col in GetComponentsInChildren<Collider2D>())
            {
                if (col != null && col.OverlapPoint(worldPosition))
                    return true;
            }

            return false;
        }
    }
}
