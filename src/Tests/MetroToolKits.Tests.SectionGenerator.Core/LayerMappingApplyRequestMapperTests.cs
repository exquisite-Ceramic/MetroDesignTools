using FluentAssertions;
using MetroToolKits.SectionGenerator.App.Support;
using MetroToolKits.SectionGenerator.Contracts.LayerMapping;

namespace MetroToolKits.Tests.SectionGenerator.Core;

public class LayerMappingApplyRequestMapperTests
{
    private readonly LayerMappingApplyRequestMapper _mapper = new();

    [Fact]
    public void ToRequest_MapsMappingCollection()
    {
        var mappings = new[]
        {
            new LayerMappingDto
            {
                LayerName = "MK_Wall",
                TypeName = "墙",
                TypeId = "Wall",
                TargetLayerPrefix = "MK_结构墙",
                ColorIndex = 1,
                TemplateId = "wall-template-1",
                TemplateName = "墙模板"
            },
            new LayerMappingDto
            {
                LayerName = "MK_Slab",
                TypeName = "板",
                TypeId = "Slab",
                TargetLayerPrefix = "MK_板",
                ColorIndex = 2,
                TemplateId = "slab-template-1",
                TemplateName = "板模板"
            }
        };

        var request = _mapper.ToRequest(mappings, applyToEntireDrawing: true);

        request.LayerAssignments.Should().HaveCount(2);
        request.LayerAssignments[0].SourceLayerName.Should().Be("MK_Wall");
        request.LayerAssignments[0].TypeId.Should().Be("Wall");
        request.LayerAssignments[0].TemplateId.Should().Be("wall-template-1");
        request.LayerAssignments[1].SourceLayerName.Should().Be("MK_Slab");
        request.LayerAssignments[1].TypeId.Should().Be("Slab");
        request.LayerAssignments[1].TemplateId.Should().Be("slab-template-1");
    }

    [Fact]
    public void ToRequest_PreservesApplyToEntireDrawing()
    {
        var request = _mapper.ToRequest(
            new[]
            {
                new LayerMappingDto
                {
                    LayerName = "MK_Wall",
                    TypeId = "Wall"
                }
            },
            applyToEntireDrawing: false);

        request.ApplyToEntireDrawing.Should().BeFalse();
    }

    [Fact]
    public void ToRequest_PreservesNullTemplateId()
    {
        var request = _mapper.ToRequest(
            new[]
            {
                new LayerMappingDto
                {
                    LayerName = "MK_Wall",
                    TypeId = "Wall",
                    TemplateId = null
                }
            },
            applyToEntireDrawing: true);

        request.LayerAssignments.Should().ContainSingle();
        request.LayerAssignments[0].TemplateId.Should().BeNull();
    }

    [Fact]
    public void ToRequest_AllowsEmptyMappings()
    {
        var request = _mapper.ToRequest(Array.Empty<LayerMappingDto>(), applyToEntireDrawing: true);

        request.ApplyToEntireDrawing.Should().BeTrue();
        request.LayerAssignments.Should().BeEmpty();
    }

    [Fact]
    public void ToDomain_MapsRequestCorrectly()
    {
        var request = new ApplyLayerMappingsRequestDto
        {
            ApplyToEntireDrawing = true,
            LayerAssignments =
            [
                new LayerTypeAssignmentDto
                {
                    SourceLayerName = "MK_Wall",
                    TypeId = "Wall",
                    TemplateId = "wall-template-1"
                },
                new LayerTypeAssignmentDto
                {
                    SourceLayerName = "MK_Slab",
                    TypeId = "Slab",
                    TemplateId = null
                }
            ]
        };

        var domainRequest = _mapper.ToDomain(request);

        domainRequest.ApplyToEntireDrawing.Should().BeTrue();
        domainRequest.LayerMappings.Should().HaveCount(2);
        domainRequest.LayerMappings.Select(mapping => mapping.SourceLayerName)
            .Should().Equal("MK_Wall", "MK_Slab");
        domainRequest.LayerMappings.Select(mapping => mapping.TypeId)
            .Should().Equal("Wall", "Slab");
        domainRequest.LayerMappings.Select(mapping => mapping.TemplateId)
            .Should().Equal("wall-template-1", null);
    }
}
