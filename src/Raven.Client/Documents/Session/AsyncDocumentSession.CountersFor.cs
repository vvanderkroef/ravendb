//-----------------------------------------------------------------------
// <copyright file="DocumentSession.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Raven.Client.Documents.Session
{
    /// <summary>
    /// Implements Unit of Work for accessing the RavenDB server
    /// </summary>
    public partial class AsyncDocumentSession
    {
        public AsyncSessionDocumentCounters CountersFor(string documentId)
        {
            return new AsyncSessionDocumentCounters(this, documentId);
        }

        public AsyncSessionDocumentCounters CountersFor(object entity)
        {
            return new AsyncSessionDocumentCounters(this, entity);
        }
    }
}
