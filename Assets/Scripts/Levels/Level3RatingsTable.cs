using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MadFact.Telemetry;

namespace MadFact
{
    /// <summary>
    /// Level 3 introduces explicit 0-to-5-star ratings as a user-by-movie table.
    /// Four profile tabs reveal one row at a time, then a short multiple-choice quiz
    /// asks students to read row extremes and column averages.
    /// </summary>
    public sealed class Level3RatingsTable : MonoBehaviour
    {
        public const int ProfileCount = 4;
        public const int MovieCount = 5;
        public const int QuestionCount = ProfileCount * 2 + 2;

        static readonly string[] ProfileNames = { "WENDELL", "DOT", "HANK", "PRIYA" };

        // Hand-authored integers keep every row and the overall averages unambiguous.
        // Including zero makes the full 0-to-5 rating scale visible in the lesson.
        static readonly int[,] Ratings =
        {
            { 5, 4, 0, 3, 2 },
            { 1, 4, 5, 2, 0 },
            { 4, 5, 3, 2, 1 },
            { 3, 2, 0, 5, 4 }
        };

        const string IntroFlag = "ratings_table_intro";
        const string CompleteFlag = "ratings_table_complete";

        [SerializeField] GameObject _root;
        [SerializeField] RectTransform _profileStrip;
        [SerializeField] RectTransform _ratingsRow;
        [SerializeField] Text _rowTitle;
        [SerializeField] Text _question;
        [SerializeField] Text _progress;
        [SerializeField] Text _feedback;
        [SerializeField] Button[] _profileButtons = new Button[ProfileCount];
        [SerializeField] Image[] _profileIcons = new Image[ProfileCount];
        [SerializeField] Image[] _profileHighlights = new Image[ProfileCount];
        [SerializeField] Image[] _posters = new Image[MovieCount];
        [SerializeField] Text[] _movieTitles = new Text[MovieCount];
        [SerializeField] Text[] _ratingLabels = new Text[MovieCount];
        [SerializeField] Button[] _answerButtons = new Button[4];
        [SerializeField] Text[] _answerLabels = new Text[4];

        int _selectedProfile;
        int _questionIndex;
        int[] _visibleOptions = new int[4];
        bool _answered;
        bool _completed;
        Coroutine _advanceRoutine;

        public static string ProfileName(int profile) =>
            ProfileNames[Mathf.Clamp(profile, 0, ProfileCount - 1)];

        public static int RatingFor(int profile, int movie) =>
            Ratings[Mathf.Clamp(profile, 0, ProfileCount - 1), Mathf.Clamp(movie, 0, MovieCount - 1)];

        public static float AverageForMovie(int movie)
        {
            movie = Mathf.Clamp(movie, 0, MovieCount - 1);
            float total = 0f;
            for (int profile = 0; profile < ProfileCount; profile++)
                total += Ratings[profile, movie];
            return total / ProfileCount;
        }

        public static int FavoriteForProfile(int profile) => ExtremeForProfile(profile, true);
        public static int LeastFavoriteForProfile(int profile) => ExtremeForProfile(profile, false);
        public static int OverallFavorite() => OverallExtreme(true);
        public static int OverallLeastFavorite() => OverallExtreme(false);

        public static string Stars(int rating)
        {
            rating = Mathf.Clamp(rating, 0, 5);
            return new string('\u2605', rating) + new string('\u2606', 5 - rating);
        }

        static int ExtremeForProfile(int profile, bool highest)
        {
            profile = Mathf.Clamp(profile, 0, ProfileCount - 1);
            int bestIndex = 0;
            int bestValue = Ratings[profile, 0];
            for (int movie = 1; movie < MovieCount; movie++)
            {
                int candidate = Ratings[profile, movie];
                if ((highest && candidate > bestValue) || (!highest && candidate < bestValue))
                {
                    bestValue = candidate;
                    bestIndex = movie;
                }
            }
            return bestIndex;
        }

        static int OverallExtreme(bool highest)
        {
            int bestIndex = 0;
            float bestValue = AverageForMovie(0);
            for (int movie = 1; movie < MovieCount; movie++)
            {
                float candidate = AverageForMovie(movie);
                if ((highest && candidate > bestValue) || (!highest && candidate < bestValue))
                {
                    bestValue = candidate;
                    bestIndex = movie;
                }
            }
            return bestIndex;
        }

        void Awake()
        {
            BindButtons();
            ApplyRuntimeArtwork();
        }

        public static Level3RatingsTable Create(Transform canvas)
        {
            var go = UIFactory.Node(canvas, "Level3Ratings");
            UIFactory.Fill(UIFactory.RT(go));
            var level = go.AddComponent<Level3RatingsTable>();
            level.Build(go.transform);
            level.BindButtons();
            return level;
        }

        void Build(Transform parent)
        {
            _root = UIFactory.Image(parent, "Level3RatingsTable", new Color(0f, 0f, 0f, 0.42f)).gameObject;
            UIFactory.Fill(UIFactory.RT(_root), 0, 46, 0, 0);

            var window = UIFactory.DialogWindow(_root.transform, "RatingsWindow", Theme.Face);
            UIFactory.Place(UIFactory.RT(window.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(914, 466), new Vector2(0, -9));

            var title = UIFactory.Text(window.transform, "Title", "CUSTOMER RATINGS TABLE", 21,
                Theme.CrtAmber, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(title.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(520, 30), new Vector2(24, -5));
            var scale = UIFactory.Text(window.transform, "Scale", "0 = DID NOT LIKE     5 = LOVED IT", 11,
                Theme.TitleText, Theme.SystemSans, TextAnchor.MiddleRight, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(scale.gameObject), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(330, 26), new Vector2(-24, -7));

            BuildProfiles(window.transform);
            BuildRatingsRow(window.transform);
            BuildQuestionPanel(window.transform);

            _root.SetActive(false);
        }

        void BuildProfiles(Transform window)
        {
            var strip = UIFactory.Bevel(window, "ProfileStrip", Theme.FaceShade, true);
            UIFactory.Place(UIFactory.RT(strip.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(866, 70), new Vector2(24, -42));
            _profileStrip = UIFactory.RT(strip.gameObject);

            for (int profile = 0; profile < ProfileCount; profile++)
            {
                int selected = profile;
                var button = UIFactory.Button(strip.transform, "Profile" + profile, ProfileNames[profile],
                    () => SelectProfile(selected), Theme.Face, 13, Theme.SystemSans, Theme.TitleText);
                UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(0, .5f), new Vector2(0, .5f),
                    new Vector2(198, 52), new Vector2(12 + profile * 211, 0));
                _profileIcons[profile] = UIFactory.ButtonIcon(button,
                    ArtSprites.CustomerPortrait(ProfileNames[profile]), 42f);
                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleCenter;
                label.fontStyle = FontStyle.Bold;

                var selectedFrame = UIFactory.Image(button.transform, "Selected", new Color(1f, .72f, .12f, .32f),
                    Theme.Solid, Image.Type.Simple, false);
                UIFactory.Fill(UIFactory.RT(selectedFrame.gameObject), 2, 2, 2, 2);
                selectedFrame.transform.SetAsFirstSibling();
                selectedFrame.gameObject.SetActive(false);

                _profileButtons[profile] = button;
                _profileHighlights[profile] = selectedFrame;
            }
        }

        void BuildRatingsRow(Transform window)
        {
            var row = UIFactory.Bevel(window, "RatingsRow", new Color(.93f, .91f, .82f, 1f), true);
            UIFactory.Place(UIFactory.RT(row.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(866, 196), new Vector2(24, -118));
            _ratingsRow = UIFactory.RT(row.gameObject);

            _rowTitle = UIFactory.Text(row.transform, "RowTitle", "WENDELL'S RATING ROW", 14,
                Theme.Ink, Theme.Typewriter, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_rowTitle.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(820, 24), new Vector2(16, -5));

            for (int movie = 0; movie < MovieCount; movie++)
            {
                var card = UIFactory.Bevel(row.transform, "MovieColumn" + movie, Theme.FaceLight, true);
                UIFactory.Place(UIFactory.RT(card.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(156, 154), new Vector2(16 + movie * 168, -32));

                _posters[movie] = UIFactory.Image(card.transform, "Poster" + movie, Color.white,
                    ArtSprites.MatrixCover(movie), Image.Type.Simple, false);
                _posters[movie].preserveAspect = true;
                UIFactory.Place(UIFactory.RT(_posters[movie].gameObject), new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(62, 88), new Vector2(0, -7));

                _movieTitles[movie] = UIFactory.Text(card.transform, "MovieTitle" + movie,
                    GameData.MatrixMovieSet[movie].Short, 10, Theme.Ink, Theme.Typewriter,
                    TextAnchor.UpperCenter, true, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(_movieTitles[movie].gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(144, 30), new Vector2(0, 30));

                _ratingLabels[movie] = UIFactory.Text(card.transform, "Rating" + movie, "\u2605\u2605\u2605\u2605\u2605\n5 / 5", 14,
                    Theme.CrtAmber, Theme.SystemSans, TextAnchor.LowerCenter, false, FontStyle.Bold);
                UIFactory.Place(UIFactory.RT(_ratingLabels[movie].gameObject), new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(150, 34), new Vector2(0, 2));
            }
        }

        void BuildQuestionPanel(Transform window)
        {
            var panel = UIFactory.Bevel(window, "QuestionPanel", new Color(.05f, .10f, .08f, 1f), true);
            UIFactory.Place(UIFactory.RT(panel.gameObject), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(866, 126), new Vector2(24, 18));

            _progress = UIFactory.Text(panel.transform, "Progress", "TUTORIAL", 11, Theme.CrtGreenDim,
                Theme.SystemSans, TextAnchor.UpperLeft, false, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_progress.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(170, 18), new Vector2(12, -8));

            _question = UIFactory.Text(panel.transform, "Question", "Click each profile to inspect one row of ratings.",
                15, Theme.TitleText, Theme.Typewriter, TextAnchor.UpperLeft, true, FontStyle.Bold);
            UIFactory.Place(UIFactory.RT(_question.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(410, 54), new Vector2(12, -28));

            _feedback = UIFactory.Text(panel.transform, "Feedback", "Mr. Pellings will ask ten table-reading questions.",
                11, Theme.CrtAmber, Theme.SystemSans, TextAnchor.LowerLeft, true);
            UIFactory.Place(UIFactory.RT(_feedback.gameObject), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(410, 32), new Vector2(12, 8));

            for (int option = 0; option < 4; option++)
            {
                int picked = option;
                var button = UIFactory.Button(panel.transform, "Answer" + option, "MOVIE",
                    () => ChooseAnswer(picked), Theme.Face, 11, Theme.SystemSans, Theme.TitleText);
                float x = 440 + (option % 2) * 204;
                float y = -12 - (option / 2) * 52;
                UIFactory.Place(UIFactory.RT(button.gameObject), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(192, 42), new Vector2(x, y));
                _answerButtons[option] = button;
                _answerLabels[option] = button.GetComponentInChildren<Text>();
                _answerLabels[option].fontStyle = FontStyle.Bold;
            }
        }

        void BindButtons()
        {
            for (int profile = 0; profile < _profileButtons.Length; profile++)
            {
                if (_profileButtons[profile] == null) continue;
                int selected = profile;
                _profileButtons[profile].onClick.RemoveAllListeners();
                _profileButtons[profile].onClick.AddListener(() => SelectProfile(selected));
            }
            for (int option = 0; option < _answerButtons.Length; option++)
            {
                if (_answerButtons[option] == null) continue;
                int picked = option;
                _answerButtons[option].onClick.RemoveAllListeners();
                _answerButtons[option].onClick.AddListener(() => ChooseAnswer(picked));
            }
        }

        /// <summary>
        /// Customer portraits and fictional covers are generated sprites, so Unity cannot
        /// serialize them into the authored prefab. Restore them whenever the scene loads.
        /// </summary>
        void ApplyRuntimeArtwork()
        {
            for (int profile = 0; profile < ProfileCount; profile++)
            {
                if (_profileIcons[profile] == null && _profileButtons[profile] != null)
                    _profileIcons[profile] = UIFactory.FindDeep<Image>(_profileButtons[profile].transform, "Icon");
                if (_profileIcons[profile] != null)
                    _profileIcons[profile].sprite = ArtSprites.CustomerPortrait(ProfileNames[profile]);
            }

            for (int movie = 0; movie < MovieCount; movie++)
                if (_posters[movie] != null)
                    _posters[movie].sprite = ArtSprites.MatrixCover(movie);
        }

        public void Open()
        {
            if (_root == null) return;
            _root.SetActive(true);
            _questionIndex = 0;
            _answered = false;
            _completed = false;
            SetAnswersInteractable(false);
            SelectProfile(0, false);

            if (GameManager.I != null && !GameManager.I.Run.HasFlag(IntroFlag))
            {
                GameManager.I.Run.SetFlag(IntroFlag);
                _progress.text = "TUTORIAL";
                _question.text = "Click each profile to inspect one row of ratings.";
                _feedback.text = "Ratings use zero to five stars.";
                MadFactBootstrap.I.Comms.ShowFocused(Speaker.OldDude, new[]
                {
                    "Rigid rules missed what individual customers wanted. Ratings let people score movies they watched from zero to five stars.",
                    "Each movie is one column. Click a profile, read the stars, then answer my questions."
                }, new[] { _profileStrip, _ratingsRow }, BeginQuiz);
            }
            else
            {
                BeginQuiz();
            }
        }

        public void Close()
        {
            if (_advanceRoutine != null)
            {
                StopCoroutine(_advanceRoutine);
                _advanceRoutine = null;
            }
            if (_root != null) _root.SetActive(false);
        }

        void BeginQuiz()
        {
            _questionIndex = 0;
            ShowQuestion();
        }

        void ShowQuestion()
        {
            if (_completed) return;
            if (_questionIndex >= QuestionCount)
            {
                CompleteLevel();
                return;
            }

            _answered = false;
            bool overall = _questionIndex >= ProfileCount * 2;
            bool highest = (_questionIndex % 2) == 0;
            int profile = overall ? -1 : _questionIndex / 2;

            if (overall)
            {
                // Keep individual customer rows visible. Students must click through the
                // four profiles and work out the column average instead of being shown it.
                SelectProfile(0, false);
                _question.text = highest
                    ? "Check all four customers. Which movie has the highest average rating?"
                    : "Check all four customers. Which movie has the lowest average rating?";
            }
            else
            {
                SelectProfile(profile, false);
                _question.text = $"Which movie did {ProfileNames[profile]} rate the {(highest ? "highest" : "lowest")}?";
            }

            int correct = overall
                ? (highest ? OverallFavorite() : OverallLeastFavorite())
                : (highest ? FavoriteForProfile(profile) : LeastFavoriteForProfile(profile));
            BuildOptions(correct);
            _progress.text = $"QUESTION {_questionIndex + 1} / {QuestionCount}";
            _feedback.text = overall
                ? "Click each profile. Add a movie's four ratings, then divide by four."
                : "Compare the filled stars in this customer's row.";
            _feedback.color = Theme.CrtAmber;
            SetAnswersInteractable(true);
        }

        void BuildOptions(int correct)
        {
            var choices = new List<int> { 0, 1, 2, 3, 4 };
            int omitted = (_questionIndex + 2) % MovieCount;
            if (omitted == correct) omitted = (omitted + 1) % MovieCount;
            choices.Remove(omitted);

            int rotation = _questionIndex % choices.Count;
            for (int option = 0; option < _visibleOptions.Length; option++)
            {
                int movie = choices[(option + rotation) % choices.Count];
                _visibleOptions[option] = movie;
                _answerLabels[option].text = GameData.MatrixMovieSet[movie].Short;
            }
        }

        void ChooseAnswer(int option)
        {
            if (_answered || _completed || option < 0 || option >= _visibleOptions.Length) return;

            bool overall = _questionIndex >= ProfileCount * 2;
            bool highest = (_questionIndex % 2) == 0;
            int profile = overall ? -1 : _questionIndex / 2;
            int correct = overall
                ? (highest ? OverallFavorite() : OverallLeastFavorite())
                : (highest ? FavoriteForProfile(profile) : LeastFavoriteForProfile(profile));
            int selected = _visibleOptions[option];
            bool isCorrect = selected == correct;

            MadFactLokiLogger.Instance?.Log("level_3_rating_question_answered",
                isCorrect ? "Rating table question answered correctly" : "Rating table question answered incorrectly",
                new
                {
                    question_index = _questionIndex,
                    question_id = QuestionId(profile, highest),
                    profile = overall ? "ALL_CUSTOMERS" : ProfileNames[profile],
                    selected_movie = GameData.MatrixMovieSet[selected].Title,
                    correct_movie = GameData.MatrixMovieSet[correct].Title,
                    correct = isCorrect
                });

            if (!isCorrect)
            {
                _feedback.text = overall
                    ? "Not quite. Check all four profiles and calculate each movie's average."
                    : $"Not quite. Compare the filled stars in {ProfileNames[profile]}'s row.";
                _feedback.color = Theme.ErrorRed;
                if (AudioTension.I != null) AudioTension.I.Buzzer();
                return;
            }

            _answered = true;
            SetAnswersInteractable(false);
            _feedback.text = "Correct. You found the right movie column.";
            _feedback.color = Theme.CrtGreen;
            if (AudioTension.I != null) AudioTension.I.ChaChing();
            _advanceRoutine = StartCoroutine(AdvanceQuestion());
        }

        IEnumerator AdvanceQuestion()
        {
            yield return new WaitForSecondsRealtime(.7f);
            _advanceRoutine = null;
            _questionIndex++;
            ShowQuestion();
        }

        void CompleteLevel()
        {
            if (_completed) return;
            _completed = true;
            SetAnswersInteractable(false);
            _progress.text = "TABLE COMPLETE";
            _question.text = "You found every row result and both overall averages.";
            _feedback.text = "Many customer rows together form a user-by-movie ratings table.";
            _feedback.color = Theme.CrtGreen;
            GameManager.I?.Run.SetFlag(CompleteFlag);
            MadFactBootstrap.I.OnRatingsTableGoal();
        }

        void SelectProfile(int profile, bool logView = true)
        {
            _selectedProfile = Mathf.Clamp(profile, 0, ProfileCount - 1);
            _rowTitle.text = ProfileNames[_selectedProfile] + "'S RATING ROW";
            for (int i = 0; i < ProfileCount; i++)
            {
                if (_profileHighlights[i] != null)
                    _profileHighlights[i].gameObject.SetActive(i == _selectedProfile);
                if (_profileButtons[i] != null)
                {
                    var label = _profileButtons[i].GetComponentInChildren<Text>();
                    if (label != null) label.color = i == _selectedProfile ? Theme.Ink : Theme.TitleText;
                }
            }

            for (int movie = 0; movie < MovieCount; movie++)
            {
                int rating = Ratings[_selectedProfile, movie];
                _ratingLabels[movie].text = $"<b>{Stars(rating)}</b>\n{rating} / 5";
            }

            if (logView)
                MadFactLokiLogger.Instance?.Log("level_3_rating_profile_viewed", "Customer rating row viewed",
                    new { profile = ProfileNames[_selectedProfile] });
        }

        void SetAnswersInteractable(bool interactable)
        {
            foreach (var button in _answerButtons)
                if (button != null) button.interactable = interactable;
        }

        static string QuestionId(int profile, bool highest) =>
            profile < 0
                ? "overall_" + (highest ? "most" : "least")
                : "profile_" + profile + "_" + (highest ? "most" : "least");
    }
}
