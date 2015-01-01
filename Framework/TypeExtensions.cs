// WinterLeaf Entertainment
// Copyright (c) 2014, WinterLeaf Entertainment LLC
// 
// 
// THIS SOFTWARE IS PROVIDED BY WINTERLEAF ENTERTAINMENT LLC ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES,
//  INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL WINTERLEAF ENTERTAINMENT LLC BE LIABLE FOR ANY DIRECT, INDIRECT, 
// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
// OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH 
// DAMAGE. 

using System;
using System.Collections.Generic;
using System.Linq;

namespace Winterleaf.ProxyServer.Framework
{
    /// <summary>
    /// Extension methods for the Type class
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Loads the configuration from assembly attributes
        /// </summary>
        /// <typeparam name="T">The type of the custom attribute to find.</typeparam>
        /// <param name="typeWithAttributes">The calling assembly to search.</param>
        /// <returns>The custom attribute of type T, if found.</returns>
        public static T GetAttribute<T>(this Type typeWithAttributes) where T : Attribute
        {
            return GetAttributes<T>(typeWithAttributes).FirstOrDefault();
        }

        /// <summary>
        /// Loads the configuration from assembly attributes
        /// </summary>
        /// <typeparam name="T">The type of the custom attribute to find.</typeparam>
        /// <param name="typeWithAttributes">The calling assembly to search.</param>
        /// <returns>An enumeration of attributes of type T that were found.</returns>
        public static IEnumerable<T> GetAttributes<T>(this Type typeWithAttributes) where T : Attribute
        {
            // Try to find the configuration attribute for the default logger if it exists
            object[] configAttributes = Attribute.GetCustomAttributes(typeWithAttributes, typeof (T), false);

            // get just the first one
            if (configAttributes != null && configAttributes.Length > 0)
                {
                foreach (T attribute in configAttributes)
                    yield return attribute;
                }
        }
    }
}