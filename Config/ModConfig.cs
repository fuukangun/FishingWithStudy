namespace fishingWithStudy.Config
{
    public class ModConfig
    {
        public bool TimerEnabled { get; set; } = true;
        public int TimerSeconds { get; set; } = 25;
        public string SelectedBank { get; set; } = "default";
        public string SelectedCategory { get; set; } = "";
        public string StudyModeKeybind { get; set; } = "K";
    }
}