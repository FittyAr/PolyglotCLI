namespace PolyglotCLI
{
    public class ApplicationMode
    {
        public bool IsWebMode { get; }
        public ApplicationMode(bool isWebMode)
        {
            IsWebMode = isWebMode;
        }
    }
}
