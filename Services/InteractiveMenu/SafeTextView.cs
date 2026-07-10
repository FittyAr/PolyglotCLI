using Terminal.Gui.Editor;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Input;

namespace PolyglotCLI
{
    /// <summary>
    /// A multiline text editor built on <see cref="Terminal.Gui.Editor.Editor"/> (the official
    /// replacement for the obsolete <c>TextView</c>). Disables Tab-as-whitespace insertion so
    /// Tab moves focus between controls, and suppresses spurious ArgumentOutOfRangeExceptions
    /// from mouse events that can occur in certain terminal emulators.
    /// </summary>
    public class SafeTextView : Editor
    {
        public SafeTextView()
        {
            // ConvertTabsToSpaces=false ensures Tab navigates controls instead of inserting whitespace
            ConvertTabsToSpaces = false;
            // Enable multiline mode (equivalent to old TextView behaviour)
            Multiline = true;
        }

        protected override bool OnMouseEvent(Terminal.Gui.Input.Mouse ev)
        {
            try
            {
                return base.OnMouseEvent(ev);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                // Swallow layout edge-case exceptions that occur in some terminal emulators
                return true;
            }
        }
    }
}
