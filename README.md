## Building
I use Visual Studio 2019 Community Edition while developing this project, so if you have it available you can simply open the solution file - otherwise
you will have to add in the appropriate project files into your own solution.

For now, you will need to also include [csPixelGameEngine](../csPixelGameEngine/tree/master/csPixelGameEngine) in your solution.

### Windows
Use the EmulatorApp for the main/default project.

### Mac
Use the NESEmulatorApp for the main/detault project.

Note: This project adds another nuget package for OpenAL that is ported to work with .NET core. It appears to be a forked project that is not supported by the people
who support OpenAL.

## Running

### Windows
You must go to OpenAL.org and download the OpenAL SDK, if you do not already have it installed.

### Mac
MacOS ships with OpenAL installed by default.

### Controls
#### Emulator
Space - Start/Stop emulator
R - Reset
F - Run a single frame of emulation
C - Step one CPU instruction

#### Controller (Player 1)
A - Start
S - Select
Z - B
X - A
Arrows control D-pad

Player 2 controls not yet supported.
