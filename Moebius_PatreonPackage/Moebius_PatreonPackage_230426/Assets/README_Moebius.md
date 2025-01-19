# Readme Moebius
Hi, first of all thank you for supporting the channel through the Patreon. It is greatly appreciated!

In this archive you can find the sources for the Moebis project (from the "Moebius-style 3D Rendering" video: https://www.youtube.com/watch?v=jlKNOirh66E )

The project should open and work "absolutely flawlessly" in Unity 2022.2.6f1.

Every script should have at least a comment at the top explaining what the script does. It's not a very script heavy project, but I also added comments ("sticky notes") in the shaders.
Mainly you'll want to check out the "MoebiusFullScreen" shader and probably the "SimpleObject_SpecularNormals" shader as well.

Check out the "URP-HighFidelity-Renderer" asset also for a look at the pipeline:
- It starts with rendering objects in plain white with shade and shadows
- Then blitting that to a texture
- Rendering objects in plain white with shade but without shadows
- Then blitting that to another texture. This way we can read (and apply crosshatch to) the shade/shadows in the Moebius shader, while setting the strength of both independently
- Next we are rendering everything else
- Then applying the full screen Moebius filter (Note that if you're in the SampleScene, you can change to material to "MoebiusPassFullScreenSampleScene" for better results. You can also change to, say, "MoebiusPassFullScreenSampleScene_Dithered" to test the dithered texture version)
- Then applying the post processing full screen filter

Note: if you want to drop-in Odin Inspector into this project, remove the OdinShim.cs script and you should be good to go. There are multiple scripts that use Odin to provide inspector buttons and stuff.

Have fun with the project and if you have any questions post them in the Discord!
Cheers