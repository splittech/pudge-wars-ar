using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.AR
{
    public sealed class ArenaCameraSwitcher : IDisposable
    {
        private readonly Camera _flatCamera;
        private readonly Camera _arCamera;
        private readonly List<CameraState> _states = new();
        private bool _disposed;

        public ArenaCameraSwitcher(Camera flatCamera, Camera arCamera, IEnumerable<Camera> backgroundCameras)
        {
            _flatCamera = flatCamera;
            _arCamera = arCamera;
            _states.Add(new CameraState(flatCamera, true));
            _states.Add(new CameraState(arCamera, true));

            foreach (var camera in backgroundCameras)
            {
                if (camera != null && camera != flatCamera && camera != arCamera)
                {
                    _states.Add(new CameraState(camera, false));
                }
            }

            SetMode(false);
        }

        public void SetMode(bool useAR)
        {
            if (_disposed)
            {
                return;
            }

            Camera activeCamera = useAR ? _arCamera : _flatCamera;
            // Disable listeners before enabling the selected one to avoid overlap during a mode switch.
            foreach (var state in _states)
            {
                if (state.Listener != null)
                {
                    state.Listener.enabled = false;
                }

                if (state.Camera != null)
                {
                    state.Camera.enabled = state.Camera == activeCamera;
                }
            }

            AudioListener listener = activeCamera.GetComponent<AudioListener>();
            listener.enabled = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var state in _states)
            {
                if (state.Camera != null)
                {
                    state.Camera.enabled = state.CameraEnabled;
                }

                if (state.Listener == null)
                {
                    continue;
                }

                if (state.CreatedListener)
                {
                    state.Listener.enabled = false;
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(state.Listener);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(state.Listener);
                    }
                }
                else
                {
                    state.Listener.enabled = state.ListenerEnabled;
                }
            }
        }

        private readonly struct CameraState
        {
            public readonly Camera Camera;
            public readonly bool CameraEnabled;
            public readonly AudioListener Listener;
            public readonly bool ListenerEnabled;
            public readonly bool CreatedListener;

            public CameraState(Camera camera, bool requiresListener)
            {
                Camera = camera;
                CameraEnabled = camera.enabled;
                Listener = camera.GetComponent<AudioListener>();
                ListenerEnabled = Listener != null && Listener.enabled;
                CreatedListener = requiresListener && Listener == null;
                if (CreatedListener)
                {
                    Listener = camera.gameObject.AddComponent<AudioListener>();
                    Listener.enabled = false;
                }
            }
        }
    }
}
