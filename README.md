![I see you!](https://github.com/phanshaw/VisionConeDemo/blob/master/SourceArt/ReadmeImage.png?raw=true)


# VisionConeDemo
A demo of using URP RenderFeatures to add a vision cone rendering pass.

Move your mouse around and don't get seen!

In this project I've put together a basic vision cone renderer. 

On the gameplay side I test to see if our target is in view via raycasts and some simple range and field of view tests. 

On the GPU side I render a depth pass to mask where we can and cannot see. Finally we composite the vision cones in screen space for the final result. 

The vision cone data is passed from the `VisionConeComponent` to the `VisionConeRenderPassFeature` via the `VisionConeManager` object.

The `VisionConeRenderPassFeature` contains two passes- one that renders a tiled depth pass from each of the maximum possible vision casters (16). 

A second pass blits these to the screen using an override shader. 

To see the vision cones in action, open `VisionConeScene` and press play. 

Please see `Assets/Resources/Shaders/ScreenSpaceVisionConeAdditive.hlsl` for an example of the vision cone shader. 

Note:
The example code here is written specifically for this example project, based off of prior experience and online reference. 

References: 

Catlike Coding's spotlight shadows overview:

https://catlikecoding.com/unity/tutorials/scriptable-render-pipeline/spotlight-shadows/

OpenGL shadow mapping to get my head around depth tested shadows (again)

http://www.opengl-tutorial.org/intermediate-tutorials/tutorial-16-shadow-mapping/
