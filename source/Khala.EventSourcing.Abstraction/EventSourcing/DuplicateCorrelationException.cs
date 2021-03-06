﻿namespace Khala.EventSourcing
{
    using System;

    public class DuplicateCorrelationException : Exception
    {
        public DuplicateCorrelationException(
            Type sourceType,
            Guid sourceId,
            Guid correlationId,
            Exception innerException)
            : base(
                  GetMessage(sourceType, sourceId, correlationId),
                  Guard(innerException))
        {
            SourceType = sourceType;
            SourceId = sourceId;
            CorrelationId = correlationId;
        }

        public Type SourceType { get; }

        public Guid SourceId { get; }

        public Guid CorrelationId { get; }

        private static Exception Guard(Exception innerException)
        {
            return innerException ?? throw new ArgumentNullException(nameof(innerException));
        }

        private static string GetMessage(
            Type sourceType,
            Guid sourceId,
            Guid correlationId)
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.",
                    nameof(sourceId));
            }

            if (correlationId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(correlationId)} cannot be empty.",
                    nameof(correlationId));
            }

            return "The correlation is already handled with the aggregate."
                + $" The type of the aggregate type is {sourceType},"
                + $" the identifier of the aggregate is {sourceId}"
                + $" and the identifier of the correlation is {correlationId}.";
        }
    }
}
