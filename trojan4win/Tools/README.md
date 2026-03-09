# Tools Directory

Place the following binaries in their respective subdirectories before building the installer:

## Tools/trojan/
Download trojan from https://github.com/trojan-gfw/trojan/releases
Place `trojan.exe` and all its dependencies here.

## Tools/proxifyre/
Download proxifyre from https://github.com/wiresock/proxifyre/releases
Place `proxifyre.exe` and all its dependencies here.

## Tools/ndisapi/ (optional)
Download NDISAPI from https://github.com/wiresock/ndisapi/releases
Place the driver files and installation scripts here:
- `ndisapi_install.bat` - script to install the NDISAPI driver
- `ndisapi_uninstall.bat` - script to uninstall the NDISAPI driver
- Driver files (.sys, .inf, .cat)

The installer will automatically install the NDISAPI driver during setup
and uninstall it during removal.
