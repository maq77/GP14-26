using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

using SSSP.BL.DTOs.Faces;
using SSSP.DAL.Models;
using SSSP.Infrastructure.Persistence.Interfaces;

namespace SSSP.BL.Services
{
    public interface IFaceProfileLoader
    {
        Task<IReadOnlyList<FaceProfileSnapshot>> LoadAsync(CancellationToken ct);
    }

    public sealed class FaceProfileDbLoader : IFaceProfileLoader
    {
        private readonly IUnitOfWork _uow;

        public FaceProfileDbLoader(IUnitOfWork uow)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        public async Task<IReadOnlyList<FaceProfileSnapshot>> LoadAsync(CancellationToken ct)
        {
            var repo = _uow.GetRepository<FaceProfile, Guid>();

            // NOTE: Later we will add tenant scoping:
            // .Where(p => p.OperatorId == operatorId)

            var entities = await repo.Query
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Embeddings)
                .ToListAsync(ct);

            var snapshots = new List<FaceProfileSnapshot>(entities.Count);

            foreach (var profile in entities)
            {
                if (profile == null) continue;

                var embeddingSnapshots = new List<FaceEmbeddingSnapshot>();

                if (profile.Embeddings != null)
                {
                    foreach (var emb in profile.Embeddings)
                    {
                        if (emb?.Vector == null || emb.Vector.Length == 0) continue;

                        float[] vector;
                        if (emb.Vector.Length % sizeof(float) != 0)
                        {
                            vector = Array.Empty<float>();
                        }
                        else
                        {
                            var floatCount = emb.Vector.Length / sizeof(float);
                            vector = new float[floatCount];
                            Buffer.BlockCopy(emb.Vector, 0, vector, 0, emb.Vector.Length);
                        }

                        embeddingSnapshots.Add(new FaceEmbeddingSnapshot
                        {
                            Id = emb.Id,
                            Vector = vector
                        });
                    }
                }

                snapshots.Add(new FaceProfileSnapshot
                {
                    Id = profile.Id,
                    UserId = profile.UserId,
                    UserName = profile.User?.UserName ?? "N/A",
                    FullName = profile.User?.FullName ?? "Name Unassigned",
                    IsPrimary = profile.IsPrimary,
                    CreatedAt = profile.CreatedAt,
                    Embeddings = embeddingSnapshots
                });
            }

            return snapshots;
        }
    }
}
