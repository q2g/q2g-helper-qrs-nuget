namespace Q2g.HelperQrs
{
    #region Usings
    using System;
    using System.Linq;
    #endregion

    public static class QrsUtilities
    {
        #region Public Methods
        public static string GetXrfKey(int length = 16)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqerstuvwxyz0123456789";
            var random = new Random();
            var result = new string(Enumerable.Repeat(chars, length)
                                              .Select(s => s[random.Next(s.Length)])
                                              .ToArray());
            return result;
        }
        #endregion
    }
}