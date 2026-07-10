using Terminal.Gui.Views;

namespace PolyglotCLI
{
    public class SafeTextView : TextView
    {
        public SafeTextView()
        {
            TabKeyAddsTab = false;
        }

        protected override bool OnMouseEvent(Terminal.Gui.Input.Mouse ev)
        {
            try
            {
                return base.OnMouseEvent(ev);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return true;
            }
        }
    }
}
