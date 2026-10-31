using System;
using UnityEngine;

namespace Koekatamarin
{
    public sealed class PanelAction : MonoBehaviour
    {
        public Action Click;
        public void Invoke() { Click?.Invoke(); }
    }
}
