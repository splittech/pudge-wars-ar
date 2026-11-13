namespace Game.Net
{
    public static class Ipv4Endpoint
    {
        public static bool TryValidate(string input, out string address)
        {
            address = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
                return false;
            string value = input.Trim();
            string[] parts = value.Split('.');
            if (parts.Length != 4)
                return false;
            int first = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0 || part.Length > 3 || (part.Length > 1 && part[0] == '0'))
                    return false;
                int number = 0;
                foreach (char digit in part)
                {
                    if (digit < '0' || digit > '9')
                        return false;
                    number = number * 10 + digit - '0';
                }
                if (number > 255)
                    return false;
                if (i == 0)
                    first = number;
            }
            if (first == 0 || first >= 224)
                return false;
            address = value;
            return true;
        }
    }
}
