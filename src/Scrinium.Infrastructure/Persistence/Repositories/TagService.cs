using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Scrinium.Core.Domain;
using Scrinium.Core.Enums;
using Scrinium.Core.Ports;

namespace Scrinium.Infrastructure.Persistence.Repositories;

public sealed class TagService : ITagService
{
  private readonly ScriniumDbContext _dbContext;

  public TagService(ScriniumDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task ApplyTagsAsync(
    Guid documentId,
    IEnumerable<string> tagNames,
    TagSource source,
    Guid appliedBy,
    CancellationToken cancellationToken)
  {
    string[] normalizedNames = tagNames
      .Select(NormalizeTagName)
      .Where(x => x.Length > 0)
      .Distinct(StringComparer.Ordinal)
      .ToArray();

    if (normalizedNames.Length == 0)
    {
      return;
    }

    List<Tag> existingTags = await _dbContext.Tags
      .Where(x => normalizedNames.Contains(x.Name))
      .ToListAsync(cancellationToken);

    Dictionary<string, Tag> tagsByName = existingTags.ToDictionary(x => x.Name, StringComparer.Ordinal);
    DateTimeOffset now = DateTimeOffset.UtcNow;

    foreach (string name in normalizedNames)
    {
      if (!tagsByName.TryGetValue(name, out Tag? tag))
      {
        tag = new Tag
        {
          Id = Guid.NewGuid(),
          Name = name,
          DisplayName = name,
          CreatedBy = appliedBy,
          CreatedAt = now,
        };

        _dbContext.Tags.Add(tag);
        tagsByName[name] = tag;
      }

      bool alreadyApplied = await _dbContext.DocumentTags
        .AnyAsync(
          x => x.DocumentId == documentId && x.TagId == tag.Id,
          cancellationToken);

      if (alreadyApplied)
      {
        continue;
      }

      _dbContext.DocumentTags.Add(new DocumentTag
      {
        DocumentId = documentId,
        TagId = tag.Id,
        Source = source,
        AppliedAt = now,
      });
    }

    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private static string NormalizeTagName(string tagName)
  {
    return tagName.Trim().ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal);
  }
}
