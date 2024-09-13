# PlatformerController2D for C# targeting Godot 4.x
A Godot >=4.2 C# implementation of the PlatformerController2D found in the AssetStore:

## Original description:
Submitted by user evpevdev; MIT; 2023-05-07

https://godotengine.org/asset-library/asset/1062

Ported from [Evan Barac's Godot 4.0 version](https://github.com/Ev01/PlatformerController2D),

This is a platformer class with many tweakable settings which can be used to control a 2D character (think supermario 1).

## Features
- Double jump
- Coyote time
- Jump buffer
- Hold jump to go higher
- Defining jump height and duration (as opposed to setting gravity and jump velocity)
- Assymetrical jumps (falling faster than rising)

## Why should you use the C# version?
- If you need to interact with the script from your C# code you'll need the C# version of it. Currently, there is no easy way to perform this:
[Referencing a custom GDScript class from C# - Can it be done?!?!](https://www.reddit.com/r/godot/comments/12um6jr/referencing_a_custom_gdscript_class_from_c_can_it/)
[Cross-language scripting](https://docs.godotengine.org/en/stable/tutorials/scripting/cross_language_scripting.html#accessing-fields)
- If you want your project to be fully written in C# and you need platform controller. 
- [You, the reader, place any other reason here]

## License
[MIT](https://opensource.org/licenses/MIT).
