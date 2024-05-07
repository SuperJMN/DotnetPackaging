namespace Zafiro.FileSystem.Unix;

public static class UnixFilePermissionsMixin
{
    public static string ToFileModeString(this UnixFilePermissions permissions)
    {
        int userPermissions = ((int)permissions & (int)UnixFilePermissions.UserPermissions) >> 0;
        int groupPermissions = ((int)permissions & (int)UnixFilePermissions.GroupPermissions) >> 3;
        int otherPermissions = ((int)permissions & (int)UnixFilePermissions.OtherPermissions) >> 6;
        return $"{userPermissions}{groupPermissions}{otherPermissions}";
    }    
    
    public static UnixFilePermissions ParseUnixPermissions(string octalPermissions)
    {
        if (octalPermissions.Length != 3)
            throw new ArgumentException("Se esperan exactamente tres dígitos para los permisos Unix", nameof(octalPermissions));
    
        int userPermissions = int.Parse(octalPermissions[0].ToString());
        int groupPermissions = int.Parse(octalPermissions[1].ToString());
        int otherPermissions = int.Parse(octalPermissions[2].ToString());
    
        UnixFilePermissions permissions = UnixFilePermissions.None;
    
        // Establecer permisos de usuario
        if ((userPermissions & 4) != 0) permissions |= UnixFilePermissions.Read;
        if ((userPermissions & 2) != 0) permissions |= UnixFilePermissions.Write;
        if ((userPermissions & 1) != 0) permissions |= UnixFilePermissions.Execute;
    
        // Establecer permisos de grupo
        if ((groupPermissions & 4) != 0) permissions |= UnixFilePermissions.GroupRead;
        if ((groupPermissions & 2) != 0) permissions |= UnixFilePermissions.GroupWrite;
        if ((groupPermissions & 1) != 0) permissions |= UnixFilePermissions.GroupExecute;
    
        // Establecer permisos de otros
        if ((otherPermissions & 4) != 0) permissions |= UnixFilePermissions.OtherRead;
        if ((otherPermissions & 2) != 0) permissions |= UnixFilePermissions.OtherWrite;
        if ((otherPermissions & 1) != 0) permissions |= UnixFilePermissions.OtherExecute;
    
        return permissions;
    }

}