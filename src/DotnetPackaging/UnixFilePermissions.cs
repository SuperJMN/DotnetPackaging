﻿namespace DotnetPackaging;

[Flags]
public enum UnixFilePermissions
{
    None = 0,
    Execute = 1 << 0,
    Write = 1 << 1,
    Read = 1 << 2,
    UserPermissions = Read | Write | Execute,
    GroupExecute = Execute << 3,
    GroupWrite = Write << 3,
    GroupRead = Read << 3,
    GroupPermissions = GroupRead | GroupWrite | GroupExecute,
    OtherExecute = Execute << 6,
    OtherWrite = Write << 6,
    OtherRead = Read << 6,
    OtherPermissions = OtherRead | OtherWrite | OtherExecute,
    AllPermissions = UserPermissions | GroupPermissions | OtherPermissions
}