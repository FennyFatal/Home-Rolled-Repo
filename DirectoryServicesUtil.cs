using System;
using System.DirectoryServices;
using System.Collections;
using System.DirectoryServices.AccountManagement;

public class DirectoryServicesUtil
{
    public class DomainComputer : IComparable
    {
        DirectoryEntry de;
        public DomainComputer(DirectoryEntry de)
        {
            this.de = de;
            ArrayList al = new ArrayList();
            try {
                ComputerName = de.Name.Substring(3);
            } catch { }
        }
        public override string ToString()
        {
            return ComputerName;
        }

        public string ComputerName;
        private string operatingSystem = null;
        private string operatingSystemVersion = null;
        private string operatingSystemServicePack = null;
        public string OperatingSystem
        {
            get
            {
                if (operatingSystem == null)
                    return operatingSystem = (string)de.Properties["operatingSystem"].Value;
                else 
                    return operatingSystem;
            }
        }
        public string OperatingSystemVersion
        {
            get
            {
                if (operatingSystemVersion == null)
                    return operatingSystemVersion = (string)de.Properties["operatingSystemVersion"].Value;
                else
                    return operatingSystemVersion;
            }
        }
        public string OperatingSystemServicePack
        {
            get
            {
                if (operatingSystemServicePack == null)
                    return operatingSystemServicePack = (string)de.Properties["operatingSystemServicePack"].Value;
                else
                    return operatingSystemServicePack;
            }
        }

        public int CompareTo(object obj)
        {
            return String.Compare(this.ToString(), obj.ToString());
        }
    }
    public static String getFullNameFromUsername(string userName)
    {
        using (PrincipalContext pc = new PrincipalContext(ContextType.Domain))
        {
            UserPrincipal up = UserPrincipal.FindByIdentity(pc, userName);
            return up.DisplayName;
            // or return up.GivenName + " " + up.Surname;
        }

    }
    public static System.Collections.ArrayList getComputersInDomainEx(string domainName, Counter countForm = null )
    {
        System.Collections.ArrayList computers = new System.Collections.ArrayList();
        DirectoryEntry de = new DirectoryEntry("LDAP://" + domainName);
        DirectorySearcher mySearcher = new DirectorySearcher(de);
        mySearcher.Filter = ("(objectClass=computer)");
        mySearcher.SizeLimit = int.MaxValue;
        mySearcher.PageSize = int.MaxValue;

        foreach (SearchResult c in mySearcher.FindAll())
        {
            int start = c.Path.IndexOf("CN=")+3;
            string name = c.Path.Substring(start).Split(',')[0];
                //name = c.GetDirectoryEntry().Name.Replace("CN=", "");
            computers.Add(new DomainComputer(c.GetDirectoryEntry()));
            if (countForm != null)
                countForm.oneMore();
        }
        computers.Sort();
        return computers;
    }
    public interface Counter
    {
        void oneMore();
    }
    public DirectoryServicesUtil()
    {

    }
    public static System.Collections.ArrayList getComputersInDomain(string domainName, Counter countForm = null )
    {
        System.Collections.ArrayList computers = new System.Collections.ArrayList();
        DirectoryEntry de = new DirectoryEntry("LDAP://" + domainName);
        DirectorySearcher mySearcher = new DirectorySearcher(de);
        mySearcher.Filter = ("(objectClass=computer)");
        mySearcher.SizeLimit = int.MaxValue;
        mySearcher.PageSize = int.MaxValue;

        foreach (SearchResult c in mySearcher.FindAll())
        {
            int start = c.Path.IndexOf("CN=")+3;
            string name = c.Path.Substring(start).Split(',')[0];
                //name = c.GetDirectoryEntry().Name.Replace("CN=", "");
            computers.Add(name);
            if (countForm != null)
                countForm.oneMore();
        }
        computers.Sort();
        return computers;
    }
    public static bool isValidAccount(string domainName, string username, string password)
    {
        return IsAuthenticated("LDAP://" + domainName, username, password);
    }
    public static bool IsAuthenticated(string srvr, string usr, string pwd)
    {
        bool authenticated = false;

        try
        {
            DirectoryEntry entry = new DirectoryEntry(srvr, usr, pwd);
            object nativeObject = entry.NativeObject;
            authenticated = true;
        }
        catch (DirectoryServicesCOMException)
        {
            //not authenticated; reason why is in cex
        }
        catch (Exception)
        {
            //not authenticated due to some other exception [this is optional]
        }
        return authenticated;
    }
}
