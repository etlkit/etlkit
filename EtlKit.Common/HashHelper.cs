using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EtlKit.Primitives;

namespace EtlKit.Common
{
    public static class HashHelper
    {
        public static string Encrypt_Char40(string text)
        {
            if (text == null)
                return string.Empty;

            var hexBuilder = new StringBuilder();
            using var hashManager = new SHA1Managed();
            var hashValue = hashManager.ComputeHash(Encoding.UTF8.GetBytes(text));

            foreach (var hashByte in hashValue)
                hexBuilder.Append(hashByte.ToString("x2"));

            return hexBuilder.ToString().ToUpper();
        }

        public static string Encrypt_Char40(ITask task) =>
            Encrypt_Char40(task.TaskName + "|" + task.TaskType);

        public static string Encrypt_Char40(ITask task, string id) =>
            Encrypt_Char40(task.TaskName + "|" + task.TaskType + "|" + id);

        public static string RandomString(int length)
        {
            var random = new Random();
            const string pool = "abcdefghijklmnopqrstuvwxyz0123456789";
            var chars = Enumerable.Range(0, length).Select(_ => pool[random.Next(0, pool.Length)]);
            return new string(chars.ToArray());
        }
    }
}
