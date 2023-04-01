using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentValidation;
using FluentValidation.Results;
using Mass.Transit.Outbox.Repo.Replicate.test.TestFramework;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Consumers;

public static class BlobStore
{
    public class Consumer : IConsumer<Query>
    {
        private readonly IValidator<Query> _validator;
        private readonly ILogger<Consumer> _logger;

        public Consumer(IValidator<Query> validator, ILogger<Consumer> logger)
        {
            _validator = validator;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Query> context)
        {
            try
            {
                var message = context.Message;
                var validation = await _validator.ValidateAsync(message, context.CancellationToken);
                if (!validation.IsValid)
                {
                    throw new Validator.ValidationException(message.Message, validation.Errors);
                }
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(message.Message));
                var client = new BlobServiceClient (AzureRiteStorageFixture.StorageConnectionString);
                var blobContainerClient = client.GetBlobContainerClient(ContainerName);
                await blobContainerClient.CreateIfNotExistsAsync();
                var blobName = $"{message.InvoiceNumber}--{message.CorrelationId}.json";
                var blobClient = blobContainerClient.GetBlobClient(blobName);

                _ = await blobClient.UploadAsync(ms, new BlobHttpHeaders { ContentType = MediaTypeNames.Application.Json});
            
                await context.RespondAsync(new Response(context.CorrelationId!.Value , $"{blobContainerClient.Name}/{blobName}"));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Blob Storage Failed");
                throw;
            }
        }

        public string ContainerName => "test";
    }

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.InvoiceNumber).NotNull();
            RuleFor(x => x.Message).NotNull();
        }

        [Serializable, ExcludeFromCodeCoverage]
        public class ValidationException : Exception

        {
            public ValidationException(string blobUrl,
                IEnumerable<ValidationFailure> validationFailures)
                : base($"The blob url cannot be validated. BlobUrl: {blobUrl}. " +
                       $"Errors: {string.Join(", ", validationFailures.Select(x => x.ErrorMessage))}")
            {
            }    
        }
    }

    [ExcludeFromCodeCoverage]
    public record Query(string Message, string InvoiceNumber, Guid CorrelationId) : IConsumer;

    [ExcludeFromCodeCoverage]
    public record Response(Guid CorrelationId, string BlobUrl);


}