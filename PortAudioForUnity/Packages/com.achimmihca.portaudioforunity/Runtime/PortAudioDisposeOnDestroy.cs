using System;
using UnityEngine;

namespace PortAudioForUnity
{
    public class PortAudioDisposeOnDestroy : MonoBehaviour
    {
        private void OnDestroy()
        {
            PortAudioUtils.Dispose();
        }
    }
}
