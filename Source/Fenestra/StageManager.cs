namespace ZeroElectric.Fenestra
{
    public static class StageManager
    {
        private static volatile IStageReceiver stageReceiver;
        public static void SetStageReceiver(IStageReceiver stageSetter)
        {
            stageReceiver = stageSetter;
        }

        public static void StartStage(Stage stage)
        {
            stageReceiver.OnStageChanged(stage);
        }
        public static void SetStageText(string text)
        {
            stageReceiver.OnStageTextChanged(text);
        }

        public static void DisplayWork(string text)
        {
            stageReceiver.DisplayWork(text);
        }
        public static void ShowError(string text)
        {
            stageReceiver.ShowError(text);
        }

        public static void ClearWorkText()
        {
            stageReceiver.ClearWorkText();
        }
    }

    public interface IStageReceiver
    {
        void OnStageTextChanged(string message);
        void OnStageChanged(Stage Stage);

        void DisplayWork(string message);
        void ShowError(string message);

        void ClearWorkText();
    }

    public enum Stage
    {
        Initializing,

        LauncherUpdate,

        //

        CheckingForUpdate,
        Downloading,

        //

        Installing,
        Decompressing,
        Cleaning,

        LaunchingAPP,

        //

        BuildStarting,
        BuildingPAK,

        CompressingFiles,
        CompressingPAK
    }
}
