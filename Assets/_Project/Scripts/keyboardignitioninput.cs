using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Excavator.Engine
{
    /// <summary>
    /// Implementacion de IIgnitionInput via teclado (New Input System).
    /// C = llave de contacto | I = arranque (mantener).
    /// </summary>
    public class KeyboardIgnitionInput : MonoBehaviour, IIgnitionInput
    {
        [SerializeField] Key contactKey = Key.C;  // KeyCode → Key (New Input System)
        [SerializeField] Key startKey   = Key.I;

        public event Action ContactToggled;
        public event Action StarterEngaged;
        public event Action StarterReleased;       // "StarterReleaded" corregido

        bool starterWasHeld;

        public void Tick()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb[contactKey].wasPressedThisFrame)
                ContactToggled?.Invoke();

            bool starterHeld = kb[startKey].isPressed;
            if (starterHeld && !starterWasHeld)
                StarterEngaged?.Invoke();
            else if (!starterHeld && starterWasHeld)
                StarterReleased?.Invoke();

            starterWasHeld = starterHeld;
        }
    }
}