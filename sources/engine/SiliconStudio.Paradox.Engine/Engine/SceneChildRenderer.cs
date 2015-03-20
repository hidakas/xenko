// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using SiliconStudio.Core;
using SiliconStudio.Paradox.Effects;
using SiliconStudio.Paradox.Engine.Graphics;
using SiliconStudio.Paradox.Engine.Graphics.Composers;

namespace SiliconStudio.Paradox.Engine
{
    /// <summary>
    /// A renderer for a child scene defined by a <see cref="SceneChildComponent"/>.
    /// </summary>
    [DataContract("SceneChildRenderer")]
    [Display("Render Child Scene")]
    public sealed class SceneChildRenderer : SceneRendererBase
    {
        private SceneInstance currentSceneInstance;
        private SceneChildProcessor sceneChildProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneChildRenderer"/> class.
        /// </summary>
        public SceneChildRenderer() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneChildRenderer"/> class.
        /// </summary>
        /// <param name="sceneChild">The scene child.</param>
        public SceneChildRenderer(SceneChildComponent sceneChild)
        {
            SceneChild = sceneChild;
        }

        /// <summary>
        /// Gets or sets the scene.
        /// </summary>
        /// <value>The scene.</value>
        [DataMember(10)]
        public SceneChildComponent SceneChild { get; set; }

        /// <summary>
        /// Gets or sets the graphics compositor override, allowing to override the composition of the scene.
        /// </summary>
        /// <value>The graphics compositor override.</value>
        [DataMemberIgnore]
        public ISceneGraphicsCompositor GraphicsCompositorOverride { get; set; } // Overrides are accessible only at runtime

        protected override void Destroy()
        {
            if (GraphicsCompositorOverride != null)
            {
                GraphicsCompositorOverride.Dispose();
                GraphicsCompositorOverride = null;
            }

            base.Destroy();
        }

        protected override void DrawCore(RenderContext context, RenderFrame output)
        {
            if (SceneChild == null || !SceneChild.Enabled)
            {
                return;
            }

            currentSceneInstance = SceneInstance.GetCurrent(Context);

            sceneChildProcessor = sceneChildProcessor ?? currentSceneInstance.GetProcessor<SceneChildProcessor>();

            if (sceneChildProcessor == null)
            {
                return;
            }

            SceneInstance sceneInstance = sceneChildProcessor.GetSceneInstance(SceneChild);
            if (sceneInstance != null)
            {
                sceneInstance.Draw(context, output, GraphicsCompositorOverride);
            }
        }
    }
}