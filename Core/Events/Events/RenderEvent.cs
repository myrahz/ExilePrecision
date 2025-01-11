using ExileCore2;

namespace ExilePrecision.Core.Events.Events
{
    public class RenderEvent
    {
        public Graphics Graphics { get; set; }

        public RenderEvent(Graphics graphics)
        {
            Graphics = graphics;
        }
    }
}