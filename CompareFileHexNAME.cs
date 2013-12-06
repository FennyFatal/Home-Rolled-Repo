namespace FennyUTILS
{
    class CompareFileHEXNAME : IComparer
    {
        public int Compare(object x, object y)
        {
            string[] chunks = ((string)x).Split('\\');
            string xx = chunks[chunks.Length - 1];
            chunks = ((string)y).Split('\\');
            string yy = chunks[chunks.Length - 1];
            return int.Parse(((string)xx).Split('.')[0], System.Globalization.NumberStyles.HexNumber) - int.Parse(((string)yy).Split('.')[0], System.Globalization.NumberStyles.HexNumber);
        }
    }
}