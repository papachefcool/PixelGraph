﻿using HelixToolkit.SharpDX.Core.Model.Scene;
using HelixToolkit.SharpDX.Core.Utilities;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Wpf.SharpDX.Model;
using PixelGraph.Rendering.CubeMaps;
using PixelGraph.Rendering.Minecraft;
using System.Windows;

namespace PixelGraph.UI.Helix.Controls
{
    public class EnvironmentCube3D : Element3D, ICubeMapSource
    {
        public IMinecraftScene Scene {
            get => (IMinecraftScene)GetValue(SceneProperty);
            set => SetValue(SceneProperty, value);
        }

        public int FaceSize {
            get => (int)GetValue(FaceSizeProperty);
            set => SetValue(FaceSizeProperty, value);
        }

        private EnvironmentCubeNode EnvCubeNode => SceneNode as EnvironmentCubeNode;
        public ShaderResourceViewProxy CubeMap => EnvCubeNode?.CubeMap;
        public long LastUpdated => EnvCubeNode?.LastUpdated ?? 0;


        protected override SceneNode OnCreateSceneNode()
        {
            return new EnvironmentCubeNode();
        }

        protected override void AssignDefaultValuesToSceneNode(SceneNode core)
        {
            if (core is EnvironmentCubeNode n) {
                n.Scene = Scene;
                n.FaceSize = FaceSize;
            }

            base.AssignDefaultValuesToSceneNode(core);
        }

        public static readonly DependencyProperty SceneProperty =
            DependencyProperty.Register(nameof(Scene), typeof(IMinecraftScene), typeof(EnvironmentCube3D), new PropertyMetadata(null, (d, e) => {
                if (d is Element3DCore {SceneNode: EnvironmentCubeNode sceneNode})
                    sceneNode.Scene = (IMinecraftScene)e.NewValue;
            }));

        public static readonly DependencyProperty FaceSizeProperty =
            DependencyProperty.Register(nameof(FaceSize), typeof(int), typeof(EnvironmentCube3D), new PropertyMetadata(256, (d, e) => {
                if (d is Element3DCore {SceneNode: EnvironmentCubeNode sceneNode})
                    sceneNode.FaceSize = (int)e.NewValue;
            }));
    }
}
