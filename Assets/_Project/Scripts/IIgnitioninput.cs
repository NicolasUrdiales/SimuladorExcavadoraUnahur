using System;

namespace Excavator.Engine
{
    /// <summary>
    /// Interfaz para la fuente de encendido del motor.
    /// Permite distintas implementaciones (teclado, UI, etc.).
    /// </summary>
    public interface IIgnitionInput
    {
        event Action ContactToggled;
        event Action StarterEngaged;
        event Action StarterReleased;   // "StarterReleased" corregido

        void Tick();
    }
}