using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MadFact.Telemetry
{
    public sealed class ParticipantSetupController : MonoBehaviour
    {
        [SerializeField] TMP_InputField _displayName;
        [SerializeField] TMP_InputField _participantId;
        [SerializeField] Toggle _consent;
        [SerializeField] TMP_Text _validationMessage;
        [SerializeField] Button _continueButton;
        [SerializeField] Button _quitButton;

        bool _processing;

        void Start()
        {
            _consent.isOn = false;
            _continueButton.onClick.AddListener(TryContinue);
            _quitButton.onClick.AddListener(Quit);
            _displayName.onValueChanged.AddListener(_ => RefreshValidation(false));
            _participantId.onValueChanged.AddListener(_ => RefreshValidation(false));
            _consent.onValueChanged.AddListener(_ => RefreshValidation(false));
            _displayName.onSubmit.AddListener(_ => TryContinue());
            _participantId.onSubmit.AddListener(_ => TryContinue());
            _validationMessage.text = string.Empty;
            RefreshValidation(false);
            _displayName.Select();
            MadFactLokiLogger.Instance?.Log(
                "participant_setup_opened", "Participant setup screen opened");
        }

        void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Quit();
        }

        public void TryContinue()
        {
            if (_processing) return;
            string displayName = (_displayName.text ?? string.Empty).Trim();
            string participantId = (_participantId.text ?? string.Empty).Trim();
            string validation = MadFactSessionManager.Validate(displayName, participantId, _consent.isOn);
            if (validation != null)
            {
                _validationMessage.text = validation;
                MadFactLokiLogger.Instance?.Log(
                    "participant_setup_validation_failed",
                    "Participant setup validation failed",
                    new { reason = ValidationReason(displayName, participantId, _consent.isOn) });
                RefreshValidation(true);
                return;
            }

            _processing = true;
            _continueButton.interactable = false;
            _displayName.interactable = false;
            _participantId.interactable = false;
            _consent.interactable = false;
            StartCoroutine(StartSessionAndContinue(displayName, participantId));
        }

        IEnumerator StartSessionAndContinue(string displayName, string participantId)
        {
            try
            {
                ParticipantSession session = MadFactSessionManager.Instance.StartSession(
                    displayName, participantId, true);
                MadFactLokiLogger.Instance?.Log(
                    "session_started",
                    "Participant session started",
                    new { anonymous_participant = participantId.Length == 0 });
                _validationMessage.text = "Starting…";
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[MadFactTelemetry] Could not start participant session: " +
                    exception.GetType().Name);
                _validationMessage.text = "Unable to start the session. Please try again.";
                _processing = false;
                _displayName.interactable = true;
                _participantId.interactable = true;
                _consent.interactable = true;
                RefreshValidation(true);
                yield break;
            }

            // Give the logger a scheduling opportunity without making navigation depend
            // on the network.
            yield return new WaitForSecondsRealtime(0.15f);
            SceneManager.LoadScene(LevelSceneCatalog.MainMenu);
        }

        void RefreshValidation(bool preserveMessage)
        {
            if (_processing) return;
            string displayName = (_displayName.text ?? string.Empty).Trim();
            string participantId = (_participantId.text ?? string.Empty).Trim();
            bool valid = MadFactSessionManager.Validate(displayName, participantId, _consent.isOn) == null;
            _continueButton.interactable = valid;
            if (!preserveMessage)
            {
                if (displayName.Length > 50)
                    _validationMessage.text = "Display name must be 50 characters or fewer.";
                else if (!MadFactSessionManager.IsParticipantIdValid(participantId))
                    _validationMessage.text = "The participant ID contains unsupported characters.";
                else
                    _validationMessage.text = string.Empty;
            }
        }

        static string ValidationReason(string displayName, string participantId, bool consent)
        {
            if (displayName.Length == 0) return "display_name_missing";
            if (displayName.Length > 50) return "display_name_too_long";
            if (!MadFactSessionManager.IsParticipantIdValid(participantId)) return "participant_id_invalid";
            if (!consent) return "consent_missing";
            return "unknown";
        }

        static void Quit()
        {
            MadFactSessionManager.Instance?.EndSession("participant_quit");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
