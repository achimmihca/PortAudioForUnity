- The DLLs are a build of portaudio v19.7.0 without ASIO support.
    - PortAudio DLLs with ASIO support can be found at https://github.com/spatialaudio/portaudio-binaries 

- To build PortAudio
    - install Visual Studio
    - follow the instructions from http://portaudio.com/docs/v19-doxydocs/compile_windows.html
    - install cmake: `winget install cmake`
    - navigate to the repo: `cd <portaudio_folder>`
    - check which project files can be generated: `cmake -G`
        - let's assume we want to build with "Visual Studio 2019"
    - generate Visual Studio project files for 64 bit architecture (default): `cmake -G "Visual Studio 16 2019" -DCMAKE_INSTALL_PREFIX=/build/cmake`
    - compile Visual Studio project for release: `cmake --build . --config Release`
    - copy `Release/portaudio_x86.dll` to the correct location
    - generate Visual Studio project files for 32 bit architecture: `cmake -G "Visual Studio 16 2019" -A Win32 -DCMAKE_INSTALL_PREFIX=/build/cmake`
    - compile Visual Studio project for release: `cmake --build . --config Release`
    - copy `Release/portaudio_x64.dll` to the correct location

- The C# bindings for PortAudio are from https://github.com/atsushieno/portaudio-sharp