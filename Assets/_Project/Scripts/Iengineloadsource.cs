namespace Excavator.Engine
{
    public interface IEngineLoadSource
    {
        float ReadLoad01();

    }

    public class NullLoadSource : IEngineLoadSource
    {
        public float ReadLoad01() => 0f;
    }
 
}


