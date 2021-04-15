﻿// <auto-generated />
using System;
using IdentityServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IdentityServer.Data.Migrations.Application
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20210331214135_UnknownChange")]
    partial class UnknownChange
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.4")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("IdentityServer.Models.ApplicationUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("FamilyName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GivenName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("bit");

                    b.Property<DateTime>("RegistrationDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("bit");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex")
                        .HasFilter("[NormalizedUserName] IS NOT NULL");

                    b.ToTable("AspNetUsers");
                });

            modelBuilder.Entity("IdentityServer.Models.GrantedGroupPermission", b =>
                {
                    b.Property<long>("ResourceId")
                        .HasColumnType("bigint")
                        .HasColumnName("ResourceId");

                    b.Property<string>("PermissionId")
                        .HasColumnType("nvarchar(450)")
                        .HasColumnName("PermissionId");

                    b.Property<long>("GroupId")
                        .HasColumnType("bigint");

                    b.Property<string>("GranteeType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ResourceId", "PermissionId", "GroupId");

                    b.HasIndex("GroupId");

                    b.ToTable("GrantedGroupPermissions");

                    b.HasDiscriminator<string>("GranteeType").HasValue("Group");
                });

            modelBuilder.Entity("IdentityServer.Models.GrantedUserPermission", b =>
                {
                    b.Property<long>("ResourceId")
                        .HasColumnType("bigint")
                        .HasColumnName("ResourceId");

                    b.Property<string>("PermissionId")
                        .HasColumnType("nvarchar(450)")
                        .HasColumnName("PermissionId");

                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("GranteeType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ResourceId", "PermissionId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("GrantedUserPermissions");

                    b.HasDiscriminator<string>("GranteeType").HasValue("User");
                });

            modelBuilder.Entity("IdentityServer.Models.GroupToGroupAssignment", b =>
                {
                    b.Property<long>("ContainerGroupId")
                        .HasColumnType("bigint");

                    b.Property<long>("MemberGroupId")
                        .HasColumnType("bigint");

                    b.HasKey("ContainerGroupId", "MemberGroupId");

                    b.HasIndex("MemberGroupId");

                    b.ToTable("GroupToGroupAssignments");
                });

            modelBuilder.Entity("IdentityServer.Models.Resource", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Description")
                        .HasMaxLength(2048)
                        .HasColumnType("nvarchar(2048)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<long?>("ParentID")
                        .HasColumnType("bigint");

                    b.Property<string>("ResourceTypeId")
                        .IsRequired()
                        .HasColumnType("nvarchar(128)");

                    b.HasKey("Id");

                    b.HasIndex("ParentID");

                    b.HasIndex("ResourceTypeId");

                    b.ToTable("Resource");

                    b.HasDiscriminator<string>("ResourceTypeId").HasValue("Resource");
                });

            modelBuilder.Entity("IdentityServer.Models.ResourceType", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("Description")
                        .HasMaxLength(4096)
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("ResourceTypes");

                    b.HasData(
                        new
                        {
                            Id = "Group"
                        },
                        new
                        {
                            Id = "Volume"
                        });
                });

            modelBuilder.Entity("IdentityServer.Models.ResourceTypePermission", b =>
                {
                    b.Property<string>("ResourceTypeId")
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("PermissionId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Description")
                        .HasMaxLength(2048)
                        .HasColumnType("nvarchar(2048)");

                    b.HasKey("ResourceTypeId", "PermissionId");

                    b.ToTable("Permissions");

                    b.HasData(
                        new
                        {
                            ResourceTypeId = "Group",
                            PermissionId = "Access Manager",
                            Description = "Add/Remove group members"
                        },
                        new
                        {
                            ResourceTypeId = "Volume",
                            PermissionId = "Read"
                        },
                        new
                        {
                            ResourceTypeId = "Volume",
                            PermissionId = "Annotate"
                        },
                        new
                        {
                            ResourceTypeId = "Volume",
                            PermissionId = "Review"
                        });
                });

            modelBuilder.Entity("IdentityServer.Models.UserToGroupAssignment", b =>
                {
                    b.Property<long>("GroupId")
                        .HasColumnType("bigint");

                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("GroupId", "UserId");

                    b.HasIndex("UserId");

                    b.ToTable("UserToGroupAssignments");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Discriminator")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex")
                        .HasFilter("[NormalizedName] IS NOT NULL");

                    b.ToTable("AspNetRoles");

                    b.HasDiscriminator<string>("Discriminator").HasValue("IdentityRole");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderKey")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("RoleId")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Value")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens");
                });

            modelBuilder.Entity("IdentityServer.Models.Group", b =>
                {
                    b.HasBaseType("IdentityServer.Models.Resource");

                    b.HasDiscriminator().HasValue("Group");

                    b.HasData(
                        new
                        {
                            Id = -1L,
                            Name = "Everyone"
                        });
                });

            modelBuilder.Entity("IdentityServer.Models.ApplicationRole", b =>
                {
                    b.HasBaseType("Microsoft.AspNetCore.Identity.IdentityRole");

                    b.HasDiscriminator().HasValue("ApplicationRole");

                    b.HasData(
                        new
                        {
                            Id = "904f0342-6732-469d-b5e3-642272aa9391",
                            ConcurrencyStamp = "01963897-1dec-4736-81f9-8b90872cbcaf",
                            Name = "Administrator",
                            NormalizedName = "Administrator"
                        });
                });

            modelBuilder.Entity("IdentityServer.Models.GrantedGroupPermission", b =>
                {
                    b.HasOne("IdentityServer.Models.Group", "PermittedGroup")
                        .WithMany("PermissionsHeld")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.ClientCascade)
                        .IsRequired();

                    b.HasOne("IdentityServer.Models.Resource", "Resource")
                        .WithMany("GroupsWithPermissions")
                        .HasForeignKey("ResourceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("PermittedGroup");

                    b.Navigation("Resource");
                });

            modelBuilder.Entity("IdentityServer.Models.GrantedUserPermission", b =>
                {
                    b.HasOne("IdentityServer.Models.Resource", "Resource")
                        .WithMany("UsersWithPermissions")
                        .HasForeignKey("ResourceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("IdentityServer.Models.ApplicationUser", "PermittedUser")
                        .WithMany("PermissionsHeld")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientCascade)
                        .IsRequired();

                    b.Navigation("PermittedUser");

                    b.Navigation("Resource");
                });

            modelBuilder.Entity("IdentityServer.Models.GroupToGroupAssignment", b =>
                {
                    b.HasOne("IdentityServer.Models.Group", "Container")
                        .WithMany("MemberGroups")
                        .HasForeignKey("ContainerGroupId")
                        .OnDelete(DeleteBehavior.ClientCascade)
                        .IsRequired();

                    b.HasOne("IdentityServer.Models.Group", "Member")
                        .WithMany("MemberOfGroups")
                        .HasForeignKey("MemberGroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Container");

                    b.Navigation("Member");
                });

            modelBuilder.Entity("IdentityServer.Models.Resource", b =>
                {
                    b.HasOne("IdentityServer.Models.Group", "Parent")
                        .WithMany("Children")
                        .HasForeignKey("ParentID");

                    b.HasOne("IdentityServer.Models.ResourceType", "ResourceType")
                        .WithMany()
                        .HasForeignKey("ResourceTypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Parent");

                    b.Navigation("ResourceType");
                });

            modelBuilder.Entity("IdentityServer.Models.ResourceTypePermission", b =>
                {
                    b.HasOne("IdentityServer.Models.ResourceType", "ResourceType")
                        .WithMany("Permissions")
                        .HasForeignKey("ResourceTypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ResourceType");
                });

            modelBuilder.Entity("IdentityServer.Models.UserToGroupAssignment", b =>
                {
                    b.HasOne("IdentityServer.Models.Group", "Group")
                        .WithMany("MemberUsers")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("IdentityServer.Models.ApplicationUser", "User")
                        .WithMany("GroupAssignments")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Group");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("IdentityServer.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("IdentityServer.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("IdentityServer.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("IdentityServer.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("IdentityServer.Models.ApplicationUser", b =>
                {
                    b.Navigation("GroupAssignments");

                    b.Navigation("PermissionsHeld");
                });

            modelBuilder.Entity("IdentityServer.Models.Resource", b =>
                {
                    b.Navigation("GroupsWithPermissions");

                    b.Navigation("UsersWithPermissions");
                });

            modelBuilder.Entity("IdentityServer.Models.ResourceType", b =>
                {
                    b.Navigation("Permissions");
                });

            modelBuilder.Entity("IdentityServer.Models.Group", b =>
                {
                    b.Navigation("Children");

                    b.Navigation("MemberGroups");

                    b.Navigation("MemberOfGroups");

                    b.Navigation("MemberUsers");

                    b.Navigation("PermissionsHeld");
                });
#pragma warning restore 612, 618
        }
    }
}
