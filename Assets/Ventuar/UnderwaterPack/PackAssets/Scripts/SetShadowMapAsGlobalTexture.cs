/*
MIT License

Copyright (c) 2018 Gaxil

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

https://github.com/Gaxil/Unity-InteriorMapping
*/

using UnityEngine;
using UnityEngine.Rendering;

namespace LowPolyUnderwaterPack
{
    /// <summary>
    /// Low Poly Underwater Pack script that makes the main directional light's shadow maps accessible globally.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Light))]
    public class SetShadowMapAsGlobalTexture : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] private string textureSemanticName = "_SunCascadedShadowMap";
#if UNITY_EDITOR
        [SerializeField] private bool reset;
#endif

        #endregion

        #region Private Fields

        private CommandBuffer commandBuffer;
        private Light lightComponent;

        #endregion

        #region Unity Callbacks

        private void OnEnable()
        {
            lightComponent = GetComponent<Light>();
            SetupCommandBuffer();
        }

        private void OnDisable()
        {
            lightComponent.RemoveCommandBuffer(LightEvent.AfterShadowMap, commandBuffer);
            ReleaseCommandBuffer();
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (reset)
            {
                OnDisable();
                OnEnable();
                reset = false;
            }
        }
#endif

        #endregion

        #region Command Buffer Handling

        private void SetupCommandBuffer()
        {
            commandBuffer = new CommandBuffer();

            // Set the sun cascaded shadow map as a global texture
            RenderTargetIdentifier shadowMapRenderTextureIdentifier = BuiltinRenderTextureType.CurrentActive;
            commandBuffer.SetGlobalTexture(textureSemanticName, shadowMapRenderTextureIdentifier);

            lightComponent.AddCommandBuffer(LightEvent.AfterShadowMap, commandBuffer);
        }

        private void ReleaseCommandBuffer()
        {
            commandBuffer.Clear();
        }

        #endregion
    }
}