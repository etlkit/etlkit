using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EtlKit.Primitives;

namespace EtlKit.Common
{
    /// <summary>
    /// Hashing and random-string utilities used for task identity (<see cref="ITask.TaskHash"/>) and
    /// generating unique names.
    /// </summary>
    public static class HashHelper
    {
        /// <summary>
        /// Computes the SHA-1 hash of <paramref name="text"/> and returns it as a 40-character
        /// uppercase hex string.
        /// </summary>
        /// <param name="text">The text to hash, or <see langword="null"/> (returns an empty string).</param>
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

        /// <summary>
        /// Computes the default <see cref="ITask.TaskHash"/> from <paramref name="task"/>'s <see
        /// cref="ITask.TaskName"/> and <see cref="ITask.TaskType"/>.
        /// </summary>
        /// <param name="task">The task to derive a hash for.</param>
        public static string Encrypt_Char40(ITask task) =>
            Encrypt_Char40(task.TaskName + "|" + task.TaskType);

        /// <summary>
        /// Computes a hash from <paramref name="task"/>'s <see cref="ITask.TaskName"/>, <see
        /// cref="ITask.TaskType"/>, and an additional <paramref name="id"/>, for distinguishing
        /// multiple hashes derived from the same task.
        /// </summary>
        /// <param name="task">The task to derive a hash for.</param>
        /// <param name="id">Additional identifier mixed into the hash input.</param>
        public static string Encrypt_Char40(ITask task, string id) =>
            Encrypt_Char40(task.TaskName + "|" + task.TaskType + "|" + id);

        /// <summary>
        /// Generates a random string of lowercase letters and digits.
        /// </summary>
        /// <param name="length">Number of characters to generate.</param>
        public static string RandomString(int length)
        {
            var random = new Random();
            const string pool = "abcdefghijklmnopqrstuvwxyz0123456789";
            var chars = Enumerable.Range(0, length).Select(_ => pool[random.Next(0, pool.Length)]);
            return new string(chars.ToArray());
        }
    }
}
