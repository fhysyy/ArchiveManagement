#!/usr/bin/env bash
# 沙箱会剥离 ProgramFiles/ProgramData/SystemRoot/APPDATA 等变量，导致 dotnet 崩溃。
# 此脚本恢复这些变量后再调用 dotnet。
exec env \
  'ProgramFiles(x86)=C:/Program Files (x86)' \
  'ProgramFiles=C:/Program Files' \
  'ProgramData=C:/ProgramData' \
  'SystemRoot=C:/Windows' \
  'ALLUSERSPROFILE=C:/ProgramData' \
  'APPDATA=C:/Users/kenjt/AppData/Roaming' \
  'CommonProgramFiles=C:/Program Files/Common Files' \
  'CommonProgramFiles(x86)=C:/Program Files (x86)/Common Files' \
  '/c/Program Files/dotnet/dotnet.exe' "$@"
