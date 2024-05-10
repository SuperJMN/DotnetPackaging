namespace Zafiro.FileSystem.Unix;

public static class UnixFilePermissionsMixin
{
    public static UnixFileMode ToFileMode(this string modeString)
    {
        if (modeString.Length != 3)
        {
            throw new ArgumentException("El modo debe tener 3 caracteres.");
        }

        int owner = int.Parse(modeString[0].ToString());
        int group = int.Parse(modeString[1].ToString());
        int others = int.Parse(modeString[2].ToString());

        UnixFileMode mode = 0;

        // Owner permissions
        if ((owner & 4) != 0) mode |= UnixFileMode.OwnerRead;
        if ((owner & 2) != 0) mode |= UnixFileMode.OwnerWrite;
        if ((owner & 1) != 0) mode |= UnixFileMode.OwnerExecute;

        // Group permissions
        if ((group & 4) != 0) mode |= UnixFileMode.GroupRead;
        if ((group & 2) != 0) mode |= UnixFileMode.GroupWrite;
        if ((group & 1) != 0) mode |= UnixFileMode.GroupExecute;

        // Others permissions
        if ((others & 4) != 0) mode |= UnixFileMode.OthersRead;
        if ((others & 2) != 0) mode |= UnixFileMode.OthersWrite;
        if ((others & 1) != 0) mode |= UnixFileMode.OthersExecute;

        return mode;
    }
    
    public static string ToFileModeString(this UnixFileMode mode)
    {
        // Convertimos los permisos individuales
        int owner = ((mode & UnixFileMode.OwnerRead) != 0 ? 4 : 0) +
                    ((mode & UnixFileMode.OwnerWrite) != 0 ? 2 : 0) +
                    ((mode & UnixFileMode.OwnerExecute) != 0 ? 1 : 0);

        int group = ((mode & UnixFileMode.GroupRead) != 0 ? 4 : 0) +
                    ((mode & UnixFileMode.GroupWrite) != 0 ? 2 : 0) +
                    ((mode & UnixFileMode.GroupExecute) != 0 ? 1 : 0);

        int others = ((mode & UnixFileMode.OthersRead) != 0 ? 4 : 0) +
                     ((mode & UnixFileMode.OthersWrite) != 0 ? 2 : 0) +
                     ((mode & UnixFileMode.OthersExecute) != 0 ? 1 : 0);

        // Formamos la cadena de permisos como "XYZ"
        return $"{owner}{group}{others}";
    }
}