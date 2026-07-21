namespace Excavator.Engine
{
    /// <summary>
    /// Interfaz para la fuente de acelerador (throttle).
    /// Devuelve un valor normalizado 0..1.
    /// </summary>
    public interface IThrottleInput
    {
        float ReadThrottle();
    }
}
