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

namespace Winterleaf.ProxyServer
{
    public class CacheKey
    {
        public CacheKey(String requestUri, String userAgent)
        {
            AbsoluteUri = requestUri;
            UserAgent = userAgent;
        }

        public String AbsoluteUri { get; set; }
        public String UserAgent { get; set; }

        public override bool Equals(object obj)
        {
            CacheKey key = obj as CacheKey;
            if (key != null)
                return (key.AbsoluteUri == AbsoluteUri && key.UserAgent == UserAgent);
            return false;
        }

        public override int GetHashCode()
        {
            String s = AbsoluteUri + UserAgent;
            return s.GetHashCode();
        }
    }
}