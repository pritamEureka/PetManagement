using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Social;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class PetAlbumConfiguration : IEntityTypeConfiguration<PetAlbum>
{
    public void Configure(EntityTypeBuilder<PetAlbum> e)
    {
        e.ToTable("pet_albums");
        e.Property(x => x.Title).HasMaxLength(120).IsRequired();
        e.Property(x => x.CoverUrl).HasMaxLength(1000);
        e.HasIndex(x => x.PetId);
    }
}

public class PetAlbumPhotoConfiguration : IEntityTypeConfiguration<PetAlbumPhoto>
{
    public void Configure(EntityTypeBuilder<PetAlbumPhoto> e)
    {
        e.ToTable("pet_album_photos");
        e.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Caption).HasMaxLength(300);
        e.HasOne(x => x.Album).WithMany(a => a.Photos).HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.AlbumId, x.SortOrder });
        e.HasIndex(x => x.CreatedAt);
    }
}

public class PetFollowConfiguration : IEntityTypeConfiguration<PetFollow>
{
    public void Configure(EntityTypeBuilder<PetFollow> e)
    {
        e.ToTable("pet_follows");
        e.HasIndex(x => new { x.UserId, x.PetId }).IsUnique();
        e.HasIndex(x => x.PetId);
    }
}

public class PostLocationConfiguration : IEntityTypeConfiguration<PostLocation>
{
    public void Configure(EntityTypeBuilder<PostLocation> e)
    {
        e.ToTable("post_locations");
        e.Property(x => x.City).HasMaxLength(128);
        e.HasIndex(x => x.PostId).IsUnique();
        e.HasIndex(x => x.City);
        e.HasIndex(x => new { x.Latitude, x.Longitude });
    }
}
