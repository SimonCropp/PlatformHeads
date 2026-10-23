// Runs the sample library's runtime the way a head does, for sample-native.yml's run job:
//
//   sample_run turns           opens the window, rests through a second of frames, and closes it
//   sample_run lost <marker>   opens the window and rests until the X server goes, having asked to
//                              be told, and writes <marker> when it is
//
// The second never returns by itself: Xlib ends the process with exit(1) once the runtime's
// handler has called back, so the job kills the X server and then looks for the marker.
#include <cstdint>
#include <cstdio>
#include <cstring>

extern "C" {
int32_t sample_open(void);
void sample_rest(void);
void sample_on_display_lost(void (*lost)(void));
void sample_close(void);
}

namespace
{
const char* marker = nullptr;

extern "C" void DisplayLost(void)
{
    FILE* file = std::fopen(marker, "w");
    if (file != nullptr)
    {
        std::fputs("lost\n", file);
        std::fclose(file);
    }
}
}

int main(int argc, char** argv)
{
    if (argc < 2)
    {
        std::fputs("usage: sample_run turns | lost <marker>\n", stderr);
        return 2;
    }

    if (sample_open() != 1)
    {
        std::fputs("no window\n", stderr);
        return 3;
    }

    if (std::strcmp(argv[1], "turns") == 0)
    {
        for (int i = 0; i < 60; i++)
        {
            sample_rest();
        }

        sample_close();
        std::puts("turned");
        return 0;
    }

    if (std::strcmp(argv[1], "lost") == 0 &&
        argc > 2)
    {
        marker = argv[2];
        sample_on_display_lost(DisplayLost);
        std::puts("listening");
        std::fflush(stdout);
        while (true)
        {
            sample_rest();
        }
    }

    std::fputs("usage: sample_run turns | lost <marker>\n", stderr);
    return 2;
}
