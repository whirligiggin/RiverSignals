using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class SegmentCatalogService : ISegmentCatalogService
{
    private const double SeededFallbackCurrentMph = 2.5;
    private const string RaleighNeuseAccessSource = "City of Raleigh Neuse River access excerpt (user-provided)";
    private const string LegacyCatalogRiverMileSource = "Legacy segment catalog distance backfill";
    private const string UsgsGaugeRiverMileSource = "USGS station registry internal placement seed";
    private const string ProvisionalGaugeMappingConfidenceSource = "Operator-seeded provisional gauge-to-segment relationship";
    private const double RaleighNeuseRiverMileConfidence = 0.8;
    private const double LegacyCatalogRiverMileConfidence = 0.5;
    private const double UsgsGaugeRiverMileConfidence = 0.6;
    private const double ProvisionalGaugeMappingConfidence = 0.5;
    private static readonly DateTime RiverMileSeedReviewDate = new(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);
    private const string RiverMileReviewTrigger = "river-mile-spine-integration-v1 seed";

    private static readonly Guid HawRiverId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CapeFearRiverId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid NeuseRiverId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid SwiftCreekId = new("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid CrabtreeCreekId = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid WalnutCreekId = new("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid EnoRiverId = new("12121212-1212-1212-1212-121212121212");
    private static readonly Guid DeepRiverId = new("13131313-1313-1313-1313-131313131313");
    private static readonly Guid NewHopeCreekId = new("14141414-1414-1414-1414-141414141414");
    private static readonly Guid TarRiverId = new("15151515-1515-1515-1515-151515151515");
    private static readonly Guid LittleRiverNeuseBasinId = new("16161616-1616-1616-1616-161616161616");
    private static readonly Guid NewHopeRiverId = new("17171717-1717-1717-1717-171717171717");

    private static readonly Guid HawBynumAccessId = new("20111111-1111-1111-1111-111111111111");
    private static readonly Guid HawHighway64AccessId = new("20111111-1111-1111-1111-222222222222");
    private static readonly Guid HawSaxapahawAccessId = new("20111111-1111-1111-1111-333333333333");

    private static readonly Guid CapeFearBuckhornAccessId = new("20222222-2222-2222-2222-111111111111");
    private static readonly Guid CapeFearAventFerryAccessId = new("20222222-2222-2222-2222-222222222222");
    private static readonly Guid CapeFearLillingtonAccessId = new("20222222-2222-2222-2222-333333333333");
    private static readonly Guid CapeFearErwinAccessId = new("20222222-2222-2222-2222-444444444444");

    private static readonly Guid NeuseFallsDamAccessId = new("20333333-3333-3333-3333-111111111111");
    private static readonly Guid NeuseThorntonRoadAccessId = new("20333333-3333-3333-3333-121212121212");
    private static readonly Guid NeuseRiverBendParkAccessId = new("20333333-3333-3333-3333-131313131313");
    private static readonly Guid NeuseBuffaloeRoadAccessId = new("20333333-3333-3333-3333-222222222222");
    private static readonly Guid NeuseMilburnieDamAccessId = new("20333333-3333-3333-3333-333333333333");
    private static readonly Guid NeuseAndersonPointParkAccessId = new("20333333-3333-3333-3333-444444444444");
    private static readonly Guid NeusePooleRoadAccessId = new("20333333-3333-3333-3333-555555555555");
    private static readonly Guid NeuseClaytonRiverWalkAccessId = new("20333333-3333-3333-3333-666666666666");

    private static readonly Guid SwiftCreekLakeWheelerRoadAccessId = new("20444444-4444-4444-4444-111111111111");
    private static readonly Guid SwiftCreekYatesMillAreaAccessId = new("20444444-4444-4444-4444-222222222222");

    private static readonly Guid CrabtreeUmsteadGlenwoodAccessId = new("20555555-5555-5555-5555-111111111111");
    private static readonly Guid CrabtreeAndersonDriveAccessId = new("20555555-5555-5555-5555-222222222222");

    private static readonly Guid WalnutLakeJohnsonAccessId = new("20666666-6666-6666-6666-111111111111");
    private static readonly Guid WalnutNeuseConfluenceAccessId = new("20666666-6666-6666-6666-222222222222");

    private static readonly Guid EnoColeMillAccessId = new("20777777-7777-7777-7777-111111111111");
    private static readonly Guid EnoFewsFordAccessId = new("20777777-7777-7777-7777-222222222222");

    private static readonly Guid DeepFranklinvilleAccessId = new("20888888-8888-8888-8888-111111111111");
    private static readonly Guid DeepRamseurAccessId = new("20888888-8888-8888-8888-222222222222");

    private static readonly Guid NewHopeFarringtonRoadAccessId = new("20999999-9999-9999-9999-111111111111");
    private static readonly Guid NewHopeJordanLakeAccessId = new("20999999-9999-9999-9999-222222222222");

    private static readonly Guid TarLouisburgAccessId = new("20aaaaaa-aaaa-aaaa-aaaa-111111111111");
    private static readonly Guid TarFranklintonAccessId = new("20aaaaaa-aaaa-aaaa-aaaa-222222222222");

    private static readonly Guid NeuseFallsDamGaugeId = new("30111111-1111-1111-1111-111111111111");
    private static readonly Guid NeuseClaytonGaugeId = new("30111111-1111-1111-1111-222222222222");
    private static readonly Guid NeuseSmithfieldGaugeId = new("30111111-1111-1111-1111-333333333333");
    private static readonly Guid NeuseGoldsboroGaugeId = new("30111111-1111-1111-1111-444444444444");
    private static readonly Guid SwiftCreekApexGaugeId = new("30222222-2222-2222-2222-111111111111");
    private static readonly Guid CrabtreeRaleighGaugeId = new("30333333-3333-3333-3333-111111111111");
    private static readonly Guid WalnutRaleighGaugeId = new("30444444-4444-4444-4444-111111111111");
    private static readonly Guid LittleRiverZebulonGaugeId = new("30555555-5555-5555-5555-111111111111");
    private static readonly Guid EnoHillsboroughGaugeId = new("30666666-6666-6666-6666-111111111111");
    private static readonly Guid EnoDurhamGaugeId = new("30666666-6666-6666-6666-222222222222");
    private static readonly Guid HawBynumGaugeId = new("30777777-7777-7777-7777-111111111111");
    private static readonly Guid HawHawRiverGaugeId = new("30777777-7777-7777-7777-222222222222");
    private static readonly Guid HawPittsboroGaugeId = new("30777777-7777-7777-7777-333333333333");
    private static readonly Guid DeepRamseurGaugeId = new("30888888-8888-8888-8888-111111111111");
    private static readonly Guid DeepMoncureGaugeId = new("30888888-8888-8888-8888-222222222222");
    private static readonly Guid TarLouisburgGaugeId = new("30999999-9999-9999-9999-111111111111");
    private static readonly Guid TarTarboroGaugeId = new("30999999-9999-9999-9999-222222222222");
    private static readonly Guid NewHopeCreekChapelHillGaugeId = new("30aaaaaa-aaaa-aaaa-aaaa-111111111111");
    private static readonly Guid NewHopeRiverMoncureGaugeId = new("30bbbbbb-bbbb-bbbb-bbbb-111111111111");

    private static readonly Guid HawBynumTo64SegmentId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CapeFearBuckhornToAventSegmentId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid NeuseAndersonToPooleSegmentId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CapeFearLillingtonToErwinSegmentId = new("44444444-4444-4444-4444-444444444444");

    private static readonly Guid NeuseFallsDamToBuffaloeRoadSegmentId = new("55555555-5555-5555-5555-555555555555");
    private static readonly Guid NeuseFallsDamToThorntonRoadSegmentId = new("55555555-5555-5555-5555-111111111111");
    private static readonly Guid NeuseThorntonRoadToRiverBendParkSegmentId = new("55555555-5555-5555-5555-222222222222");
    private static readonly Guid NeuseRiverBendParkToBuffaloeRoadSegmentId = new("55555555-5555-5555-5555-333333333333");
    private static readonly Guid NeuseBuffaloeRoadToAndersonPointParkSegmentId = new("55555555-5555-5555-5555-444444444444");
    private static readonly Guid NeuseBuffaloeRoadToMilburnieDamSegmentId = new("66666666-6666-6666-6666-666666666666");
    private static readonly Guid NeuseMilburnieDamToAndersonPointParkSegmentId = new("77777777-7777-7777-7777-777777777777");
    private static readonly Guid NeuseAndersonPointParkToClaytonRiverWalkSegmentId = new("88888888-8888-8888-8888-888888888888");
    private static readonly Guid SwiftCreekLakeWheelerRoadToYatesMillAreaSegmentId = new("99999999-9999-9999-9999-999999999999");
    private static readonly Guid CrabtreeCreekUmsteadToAndersonDriveSegmentId = new("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid WalnutCreekLakeJohnsonToNeuseConfluenceSegmentId = new("bbbbbbbb-1111-1111-1111-111111111111");
    private static readonly Guid EnoColeMillToFewsFordSegmentId = new("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid HawSaxapahawToBynumSegmentId = new("dddddddd-1111-1111-1111-111111111111");
    private static readonly Guid DeepFranklinvilleToRamseurSegmentId = new("eeeeeeee-1111-1111-1111-111111111111");
    private static readonly Guid NewHopeCreekFarringtonRoadToJordanLakeSegmentId = new("ffffffff-1111-1111-1111-111111111111");
    private static readonly Guid TarLouisburgToFranklintonSegmentId = new("12121212-1111-1111-1111-111111111111");

    private static readonly List<River> PresetRivers =
    [
        new() { Id = HawRiverId, Name = "Haw River", State = "NC", Region = "Piedmont", Notes = "Priority paddling water with public access points." },
        new() { Id = CapeFearRiverId, Name = "Cape Fear River", State = "NC", Region = "Piedmont", Notes = "Seeded with known public access-to-access runs." },
        new() { Id = NeuseRiverId, Name = "Neuse River", State = "NC", Region = "Triangle", Notes = "Priority urban and downstream public launch corridor." },
        new() { Id = SwiftCreekId, Name = "Swift Creek", State = "NC", Region = "Triangle" },
        new() { Id = CrabtreeCreekId, Name = "Crabtree Creek", State = "NC", Region = "Triangle" },
        new() { Id = WalnutCreekId, Name = "Walnut Creek", State = "NC", Region = "Triangle" },
        new() { Id = EnoRiverId, Name = "Eno River", State = "NC", Region = "Triangle" },
        new() { Id = DeepRiverId, Name = "Deep River", State = "NC", Region = "Piedmont" },
        new() { Id = NewHopeCreekId, Name = "New Hope Creek", State = "NC", Region = "Triangle" },
        new() { Id = TarRiverId, Name = "Tar River", State = "NC", Region = "Northeast Piedmont" },
        new() { Id = LittleRiverNeuseBasinId, Name = "Little River (Neuse basin)", State = "NC", Region = "Triangle Fringe" },
        new() { Id = NewHopeRiverId, Name = "New Hope River", State = "NC", Region = "Piedmont" }
    ];

    private static readonly List<AccessPoint> PresetAccessPoints =
    [
        new() { Id = HawSaxapahawAccessId, RiverId = HawRiverId, Name = "Saxapahaw River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = HawBynumAccessId, RiverId = HawRiverId, Name = "Bynum River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 8.0 },
        new() { Id = HawHighway64AccessId, RiverId = HawRiverId, Name = "US 64 River Access", AccessType = "River Access", RiverOrder = 30, RiverMile = 14.0 },

        new() { Id = CapeFearBuckhornAccessId, RiverId = CapeFearRiverId, Name = "Buckhorn River Access", AccessType = "Boat Launch", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = CapeFearAventFerryAccessId, RiverId = CapeFearRiverId, Name = "Avent Ferry River Access", AccessType = "Boat Launch", RiverOrder = 20, RiverMile = 8.5 },
        new() { Id = CapeFearLillingtonAccessId, RiverId = CapeFearRiverId, Name = "Lillington River Access", AccessType = "Boat Launch", RiverOrder = 30, RiverMile = 18.5 },
        new() { Id = CapeFearErwinAccessId, RiverId = CapeFearRiverId, Name = "Erwin River Access", AccessType = "Boat Launch", RiverOrder = 40, RiverMile = 28.5 },

        new() { Id = NeuseFallsDamAccessId, RiverId = NeuseRiverId, Name = "Falls Dam River Access", AccessType = "Boat Launch", RiverOrder = 10, RiverMile = 0.25, Address = "12098 Old Falls of the Neuse Road", Amenities = "Trailer parking; ADA parking", SourceName = RaleighNeuseAccessSource },
        new() { Id = NeuseThorntonRoadAccessId, RiverId = NeuseRiverId, Name = "Thornton Road River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 4.5, Address = "6100 Thornton Road", SourceName = RaleighNeuseAccessSource },
        new() { Id = NeuseRiverBendParkAccessId, RiverId = NeuseRiverId, Name = "River Bend Park Kayak Launch", AccessType = "Kayak Launch", RiverOrder = 30, RiverMile = 10.0, Address = "5610 Wallace Martin Way", Amenities = "Restroom and changing area; trailer parking; ADA parking", SourceName = RaleighNeuseAccessSource },
        new() { Id = NeuseBuffaloeRoadAccessId, RiverId = NeuseRiverId, Name = "Buffaloe Road River Access", AccessType = "Boat Launch", RiverOrder = 40, RiverMile = 10.7, Address = "4901 Elizabeth Drive", SourceName = RaleighNeuseAccessSource },
        new() { Id = NeuseMilburnieDamAccessId, RiverId = NeuseRiverId, Name = "Milburnie Dam River Access", AccessType = "Boat Launch", RiverOrder = 50, RiverMile = 15.7 },
        new() { Id = NeuseAndersonPointParkAccessId, RiverId = NeuseRiverId, Name = "Anderson Point Park Boat Launch", AccessType = "Boat Launch", RiverOrder = 60, RiverMile = 16.2, Address = "20 Anderson Point Drive", Amenities = "Restrooms 3/4 to 1 mile from river access", SourceName = RaleighNeuseAccessSource },
        new() { Id = NeusePooleRoadAccessId, RiverId = NeuseRiverId, Name = "Poole Road River Access", AccessType = "Boat Launch", RiverOrder = 70, RiverMile = 17.7, Address = "6501 Poole Road", Amenities = "Trailer parking", SourceName = RaleighNeuseAccessSource },
        new() { Id = NeuseClaytonRiverWalkAccessId, RiverId = NeuseRiverId, Name = "Clayton River Walk Boat Launch", AccessType = "Boat Launch", RiverOrder = 80, RiverMile = 22.7 },

        new() { Id = SwiftCreekLakeWheelerRoadAccessId, RiverId = SwiftCreekId, Name = "Lake Wheeler Road River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = SwiftCreekYatesMillAreaAccessId, RiverId = SwiftCreekId, Name = "Yates Mill Area River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 4.0 },

        new() { Id = CrabtreeUmsteadGlenwoodAccessId, RiverId = CrabtreeCreekId, Name = "Umstead Glenwood River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = CrabtreeAndersonDriveAccessId, RiverId = CrabtreeCreekId, Name = "Anderson Drive River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 5.0 },

        new() { Id = WalnutLakeJohnsonAccessId, RiverId = WalnutCreekId, Name = "Lake Johnson River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = WalnutNeuseConfluenceAccessId, RiverId = WalnutCreekId, Name = "Neuse Confluence River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 5.0 },

        new() { Id = EnoColeMillAccessId, RiverId = EnoRiverId, Name = "Cole Mill River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = EnoFewsFordAccessId, RiverId = EnoRiverId, Name = "Few's Ford River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 5.0 },

        new() { Id = DeepFranklinvilleAccessId, RiverId = DeepRiverId, Name = "Franklinville River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = DeepRamseurAccessId, RiverId = DeepRiverId, Name = "Ramseur River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 6.0 },

        new() { Id = NewHopeFarringtonRoadAccessId, RiverId = NewHopeCreekId, Name = "Farrington Road River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = NewHopeJordanLakeAccessId, RiverId = NewHopeCreekId, Name = "Jordan Lake River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 4.0 },

        new() { Id = TarLouisburgAccessId, RiverId = TarRiverId, Name = "Louisburg River Access", AccessType = "River Access", RiverOrder = 10, RiverMile = 0.0 },
        new() { Id = TarFranklintonAccessId, RiverId = TarRiverId, Name = "Franklinton River Access", AccessType = "River Access", RiverOrder = 20, RiverMile = 8.0 }
    ];

    private static readonly List<Segment> PresetSegments =
    [
        new()
        {
            Id = HawBynumTo64SegmentId,
            RiverId = HawRiverId,
            StartAccessPointId = HawBynumAccessId,
            EndAccessPointId = HawHighway64AccessId,
            RiverOrder = 30,
            Name = "Haw River - Bynum River Access to US 64 River Access",
            DistanceMiles = 6.0,
            DefaultCurrentMph = 1.1,
            PutInName = "Bynum River Access",
            TakeOutName = "US 64 River Access"
        },
        new()
        {
            Id = CapeFearBuckhornToAventSegmentId,
            RiverId = CapeFearRiverId,
            StartAccessPointId = CapeFearBuckhornAccessId,
            EndAccessPointId = CapeFearAventFerryAccessId,
            RiverOrder = 20,
            Name = "Cape Fear River - Buckhorn River Access to Avent Ferry River Access",
            DistanceMiles = 8.5,
            DefaultCurrentMph = 1.8,
            PutInName = "Buckhorn River Access",
            TakeOutName = "Avent Ferry River Access"
        },
        new()
        {
            Id = NeuseAndersonToPooleSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseAndersonPointParkAccessId,
            EndAccessPointId = NeusePooleRoadAccessId,
            RiverOrder = 50,
            Name = "Neuse River - Anderson Point Park Boat Launch to Poole Road River Access",
            DistanceMiles = 1.5,
            DistanceSource = RaleighNeuseAccessSource,
            DefaultCurrentMph = 1.5,
            PutInName = "Anderson Point Park Boat Launch",
            TakeOutName = "Poole Road River Access"
        },
        new()
        {
            Id = CapeFearLillingtonToErwinSegmentId,
            RiverId = CapeFearRiverId,
            StartAccessPointId = CapeFearLillingtonAccessId,
            EndAccessPointId = CapeFearErwinAccessId,
            RiverOrder = 40,
            Name = "Cape Fear River - Lillington River Access to Erwin River Access",
            DistanceMiles = 10.0,
            DefaultCurrentMph = 2.0,
            PutInName = "Lillington River Access",
            TakeOutName = "Erwin River Access"
        },
        new()
        {
            Id = NeuseFallsDamToThorntonRoadSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseFallsDamAccessId,
            EndAccessPointId = NeuseThorntonRoadAccessId,
            RiverOrder = 10,
            Name = "Neuse River - Falls Dam River Access to Thornton Road River Access",
            DistanceMiles = 4.7,
            DistanceSource = RaleighNeuseAccessSource,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Falls Dam River Access",
            TakeOutName = "Thornton Road River Access"
        },
        new()
        {
            Id = NeuseThorntonRoadToRiverBendParkSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseThorntonRoadAccessId,
            EndAccessPointId = NeuseRiverBendParkAccessId,
            RiverOrder = 20,
            Name = "Neuse River - Thornton Road River Access to River Bend Park Kayak Launch",
            DistanceMiles = 5.6,
            DistanceSource = RaleighNeuseAccessSource,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Thornton Road River Access",
            TakeOutName = "River Bend Park Kayak Launch"
        },
        new()
        {
            Id = NeuseRiverBendParkToBuffaloeRoadSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseRiverBendParkAccessId,
            EndAccessPointId = NeuseBuffaloeRoadAccessId,
            RiverOrder = 30,
            Name = "Neuse River - River Bend Park Kayak Launch to Buffaloe Road River Access",
            DistanceMiles = 1.0,
            DistanceSource = RaleighNeuseAccessSource,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "River Bend Park Kayak Launch",
            TakeOutName = "Buffaloe Road River Access"
        },
        new()
        {
            Id = NeuseBuffaloeRoadToAndersonPointParkSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseBuffaloeRoadAccessId,
            EndAccessPointId = NeuseAndersonPointParkAccessId,
            RiverOrder = 40,
            Name = "Neuse River - Buffaloe Road River Access to Anderson Point Park Boat Launch",
            DistanceMiles = 6.0,
            DistanceSource = RaleighNeuseAccessSource,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Buffaloe Road River Access",
            TakeOutName = "Anderson Point Park Boat Launch"
        },
        new()
        {
            Id = NeuseFallsDamToBuffaloeRoadSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseFallsDamAccessId,
            EndAccessPointId = NeuseBuffaloeRoadAccessId,
            RiverOrder = 90,
            Name = "Neuse River - Falls Dam River Access to Buffaloe Road River Access",
            DistanceMiles = 6.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Falls Dam River Access",
            TakeOutName = "Buffaloe Road River Access",
            IsActive = false
        },
        new()
        {
            Id = NeuseBuffaloeRoadToMilburnieDamSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseBuffaloeRoadAccessId,
            EndAccessPointId = NeuseMilburnieDamAccessId,
            RiverOrder = 91,
            Name = "Neuse River - Buffaloe Road River Access to Milburnie Dam River Access",
            DistanceMiles = 5.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Buffaloe Road River Access",
            TakeOutName = "Milburnie Dam River Access"
        },
        new()
        {
            Id = NeuseMilburnieDamToAndersonPointParkSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseMilburnieDamAccessId,
            EndAccessPointId = NeuseAndersonPointParkAccessId,
            RiverOrder = 92,
            Name = "Neuse River - Milburnie Dam River Access to Anderson Point Park Boat Launch",
            DistanceMiles = 4.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Milburnie Dam River Access",
            TakeOutName = "Anderson Point Park Boat Launch"
        },
        new()
        {
            Id = NeuseAndersonPointParkToClaytonRiverWalkSegmentId,
            RiverId = NeuseRiverId,
            StartAccessPointId = NeuseAndersonPointParkAccessId,
            EndAccessPointId = NeuseClaytonRiverWalkAccessId,
            RiverOrder = 93,
            Name = "Neuse River - Anderson Point Park Boat Launch to Clayton River Walk Boat Launch",
            DistanceMiles = 6.5,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Anderson Point Park Boat Launch",
            TakeOutName = "Clayton River Walk Boat Launch"
        },
        new()
        {
            Id = SwiftCreekLakeWheelerRoadToYatesMillAreaSegmentId,
            RiverId = SwiftCreekId,
            StartAccessPointId = SwiftCreekLakeWheelerRoadAccessId,
            EndAccessPointId = SwiftCreekYatesMillAreaAccessId,
            RiverOrder = 10,
            Name = "Swift Creek - Lake Wheeler Road River Access to Yates Mill Area River Access",
            DistanceMiles = 4.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Lake Wheeler Road River Access",
            TakeOutName = "Yates Mill Area River Access"
        },
        new()
        {
            Id = CrabtreeCreekUmsteadToAndersonDriveSegmentId,
            RiverId = CrabtreeCreekId,
            StartAccessPointId = CrabtreeUmsteadGlenwoodAccessId,
            EndAccessPointId = CrabtreeAndersonDriveAccessId,
            RiverOrder = 10,
            Name = "Crabtree Creek - Umstead Glenwood River Access to Anderson Drive River Access",
            DistanceMiles = 5.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Umstead Glenwood River Access",
            TakeOutName = "Anderson Drive River Access"
        },
        new()
        {
            Id = WalnutCreekLakeJohnsonToNeuseConfluenceSegmentId,
            RiverId = WalnutCreekId,
            StartAccessPointId = WalnutLakeJohnsonAccessId,
            EndAccessPointId = WalnutNeuseConfluenceAccessId,
            RiverOrder = 10,
            Name = "Walnut Creek - Lake Johnson River Access to Neuse Confluence River Access",
            DistanceMiles = 5.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Lake Johnson River Access",
            TakeOutName = "Neuse Confluence River Access"
        },
        new()
        {
            Id = EnoColeMillToFewsFordSegmentId,
            RiverId = EnoRiverId,
            StartAccessPointId = EnoColeMillAccessId,
            EndAccessPointId = EnoFewsFordAccessId,
            RiverOrder = 10,
            Name = "Eno River - Cole Mill River Access to Few's Ford River Access",
            DistanceMiles = 5.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Cole Mill River Access",
            TakeOutName = "Few's Ford River Access"
        },
        new()
        {
            Id = HawSaxapahawToBynumSegmentId,
            RiverId = HawRiverId,
            StartAccessPointId = HawSaxapahawAccessId,
            EndAccessPointId = HawBynumAccessId,
            RiverOrder = 20,
            Name = "Haw River - Saxapahaw River Access to Bynum River Access",
            DistanceMiles = 8.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Saxapahaw River Access",
            TakeOutName = "Bynum River Access"
        },
        new()
        {
            Id = DeepFranklinvilleToRamseurSegmentId,
            RiverId = DeepRiverId,
            StartAccessPointId = DeepFranklinvilleAccessId,
            EndAccessPointId = DeepRamseurAccessId,
            RiverOrder = 10,
            Name = "Deep River - Franklinville River Access to Ramseur River Access",
            DistanceMiles = 6.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Franklinville River Access",
            TakeOutName = "Ramseur River Access"
        },
        new()
        {
            Id = NewHopeCreekFarringtonRoadToJordanLakeSegmentId,
            RiverId = NewHopeCreekId,
            StartAccessPointId = NewHopeFarringtonRoadAccessId,
            EndAccessPointId = NewHopeJordanLakeAccessId,
            RiverOrder = 10,
            Name = "New Hope Creek - Farrington Road River Access to Jordan Lake River Access",
            DistanceMiles = 4.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Farrington Road River Access",
            TakeOutName = "Jordan Lake River Access"
        },
        new()
        {
            Id = TarLouisburgToFranklintonSegmentId,
            RiverId = TarRiverId,
            StartAccessPointId = TarLouisburgAccessId,
            EndAccessPointId = TarFranklintonAccessId,
            RiverOrder = 10,
            Name = "Tar River - Louisburg River Access to Franklinton River Access",
            DistanceMiles = 8.0,
            DefaultCurrentMph = SeededFallbackCurrentMph,
            PutInName = "Louisburg River Access",
            TakeOutName = "Franklinton River Access"
        }
    ];

    private static readonly List<UsgsGauge> PresetUsgsGauges =
    [
        new() { Id = NeuseFallsDamGaugeId, RiverId = NeuseRiverId, StationId = "02087183", Name = "Neuse River | Falls Dam (Raleigh)", SourceReference = "02087183", RiverMile = 0.25, Notes = "Relevant to upper Neuse access runs near Falls Dam and Buffaloe Road." },
        new() { Id = NeuseClaytonGaugeId, RiverId = NeuseRiverId, StationId = "02087500", Name = "Neuse River | Clayton", SourceReference = "02087500", RiverMile = 22.7, Notes = "Relevant to downstream Neuse runs near Anderson Point Park and Clayton River Walk." },
        new() { Id = NeuseSmithfieldGaugeId, RiverId = NeuseRiverId, StationId = "02087570", Name = "Neuse River | Smithfield", SourceReference = "02087570", RiverMile = 40.0, Notes = "Downstream Neuse basin reference point beyond current seeded access-to-access runs." },
        new() { Id = NeuseGoldsboroGaugeId, RiverId = NeuseRiverId, StationId = "02089000", Name = "Neuse River | Goldsboro", SourceReference = "02089000", RiverMile = 80.0, Notes = "Lower Neuse reference point for later river-wide expansion." },
        new() { Id = SwiftCreekApexGaugeId, RiverId = SwiftCreekId, StationId = "02087324", Name = "Swift Creek | near Apex", SourceReference = "02087324", RiverMile = 2.0, Notes = "Relevant to Swift Creek access runs near Lake Wheeler Road and Yates Mill area." },
        new() { Id = CrabtreeRaleighGaugeId, RiverId = CrabtreeCreekId, StationId = "02087275", Name = "Crabtree Creek | at Raleigh", SourceReference = "02087275", RiverMile = 5.0, Notes = "Relevant to Crabtree Creek runs through Raleigh." },
        new() { Id = WalnutRaleighGaugeId, RiverId = WalnutCreekId, StationId = "02087300", Name = "Walnut Creek | at Raleigh", SourceReference = "02087300", RiverMile = 5.0, Notes = "Relevant to Walnut Creek runs toward the Neuse confluence." },
        new() { Id = LittleRiverZebulonGaugeId, RiverId = LittleRiverNeuseBasinId, StationId = "02087000", Name = "Little River (Neuse basin) | near Zebulon", SourceReference = "02087000", RiverMile = 0.0, Notes = "Neuse basin tributary gauge seeded as a stable hydrology anchor before any run linkage model is finalized." },
        new() { Id = EnoHillsboroughGaugeId, RiverId = EnoRiverId, StationId = "02085000", Name = "Eno River | at Hillsborough", SourceReference = "02085000", RiverMile = 0.0, Notes = "Upper Eno reference point." },
        new() { Id = EnoDurhamGaugeId, RiverId = EnoRiverId, StationId = "02085070", Name = "Eno River | near Durham", SourceReference = "02085070", RiverMile = 5.0, Notes = "Downstream Eno reference point for later run modeling." },
        new() { Id = HawBynumGaugeId, RiverId = HawRiverId, StationId = "02096500", Name = "Haw River | near Bynum", SourceReference = "02096500", RiverMile = 8.0, Notes = "Relevant to seeded Haw runs around Bynum and downstream access points." },
        new() { Id = HawHawRiverGaugeId, RiverId = HawRiverId, StationId = "02096000", Name = "Haw River | at Haw River", SourceReference = "02096000", RiverMile = 0.0, Notes = "Upstream Haw reference point for later river-wide gauge context." },
        new() { Id = HawPittsboroGaugeId, RiverId = HawRiverId, StationId = "02096700", Name = "Haw River | near Pittsboro (Moncure area influence)", SourceReference = "02096700", RiverMile = 14.0, Notes = "Lower Haw / Moncure influence reference point." },
        new() { Id = DeepRamseurGaugeId, RiverId = DeepRiverId, StationId = "02099000", Name = "Deep River | at Ramseur", SourceReference = "02099000", RiverMile = 6.0, Notes = "Relevant to seeded Deep River runs near Ramseur." },
        new() { Id = DeepMoncureGaugeId, RiverId = DeepRiverId, StationId = "02102000", Name = "Deep River | at Moncure", SourceReference = "02102000", RiverMile = 20.0, Notes = "Lower Deep River reference point near the Haw/Cape Fear system." },
        new() { Id = TarLouisburgGaugeId, RiverId = TarRiverId, StationId = "02081500", Name = "Tar River | at Louisburg", SourceReference = "02081500", RiverMile = 0.0, Notes = "Relevant to seeded Tar River runs near Louisburg." },
        new() { Id = TarTarboroGaugeId, RiverId = TarRiverId, StationId = "02083500", Name = "Tar River | at Tarboro", SourceReference = "02083500", RiverMile = 50.0, Notes = "Downstream Tar River reference point for later expansion." },
        new() { Id = NewHopeCreekChapelHillGaugeId, RiverId = NewHopeCreekId, StationId = "02097314", Name = "New Hope Creek | near Chapel Hill", SourceReference = "02097314", RiverMile = 0.0, Notes = "Relevant to New Hope Creek corridor work near Farrington Road." },
        new() { Id = NewHopeRiverMoncureGaugeId, RiverId = NewHopeRiverId, StationId = "02097517", Name = "New Hope River (Jordan Lake inflow) | near Moncure", SourceReference = "02097517", RiverMile = 0.0, Notes = "Jordan Lake inflow reference point; relationship to access-to-access runs remains intentionally non-final." }
    ];

    private static readonly List<UsgsGaugeImportTarget> PresetUsgsGaugeImportTargets =
    [
        new() { GaugeId = NeuseFallsDamGaugeId, SegmentId = NeuseFallsDamToBuffaloeRoadSegmentId, RelationshipType = UsgsGaugeRelationshipType.CorridorReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Upper Neuse corridor reference for the Falls-to-Buffaloe reach." },
        new() { GaugeId = NeuseFallsDamGaugeId, SegmentId = NeuseBuffaloeRoadToMilburnieDamSegmentId, RelationshipType = UsgsGaugeRelationshipType.CorridorReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Upper Neuse corridor reference carried downstream from the Falls gauge." },
        new() { GaugeId = NeuseFallsDamGaugeId, SegmentId = NeuseMilburnieDamToAndersonPointParkSegmentId, RelationshipType = UsgsGaugeRelationshipType.CorridorReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Upper Neuse corridor reference retained while segment-level gauge meaning remains provisional." },
        new() { GaugeId = NeuseClaytonGaugeId, SegmentId = NeuseAndersonToPooleSegmentId, RelationshipType = UsgsGaugeRelationshipType.CorridorReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Downstream Neuse corridor reference for the Anderson-to-Poole run." },
        new() { GaugeId = NeuseClaytonGaugeId, SegmentId = NeuseAndersonPointParkToClaytonRiverWalkSegmentId, RelationshipType = UsgsGaugeRelationshipType.CorridorReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Downstream Neuse corridor reference extending toward Clayton River Walk." },
        new() { GaugeId = SwiftCreekApexGaugeId, SegmentId = SwiftCreekLakeWheelerRoadToYatesMillAreaSegmentId, RelationshipType = UsgsGaugeRelationshipType.LocalReachReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Local Swift Creek reach reference near the seeded run corridor." },
        new() { GaugeId = CrabtreeRaleighGaugeId, SegmentId = CrabtreeCreekUmsteadToAndersonDriveSegmentId, RelationshipType = UsgsGaugeRelationshipType.LocalReachReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Local Crabtree Creek reach reference through Raleigh." },
        new() { GaugeId = WalnutRaleighGaugeId, SegmentId = WalnutCreekLakeJohnsonToNeuseConfluenceSegmentId, RelationshipType = UsgsGaugeRelationshipType.LocalReachReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Local Walnut Creek reach reference toward the Neuse confluence." },
        new() { GaugeId = EnoDurhamGaugeId, SegmentId = EnoColeMillToFewsFordSegmentId, RelationshipType = UsgsGaugeRelationshipType.LocalReachReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Local Eno reach reference for the seeded Durham corridor." },
        new() { GaugeId = HawBynumGaugeId, SegmentId = HawSaxapahawToBynumSegmentId, RelationshipType = UsgsGaugeRelationshipType.CorridorReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Haw corridor reference spanning the Saxapahaw-to-Bynum reach." },
        new() { GaugeId = HawBynumGaugeId, SegmentId = HawBynumTo64SegmentId, RelationshipType = UsgsGaugeRelationshipType.CorridorReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Haw corridor reference continuing downstream from Bynum." },
        new() { GaugeId = DeepRamseurGaugeId, SegmentId = DeepFranklinvilleToRamseurSegmentId, RelationshipType = UsgsGaugeRelationshipType.LocalReachReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Local Deep River reach reference near Ramseur." },
        new() { GaugeId = NewHopeCreekChapelHillGaugeId, SegmentId = NewHopeCreekFarringtonRoadToJordanLakeSegmentId, RelationshipType = UsgsGaugeRelationshipType.LocalReachReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Local New Hope Creek reach reference for the seeded corridor." },
        new() { GaugeId = TarLouisburgGaugeId, SegmentId = TarLouisburgToFranklintonSegmentId, RelationshipType = UsgsGaugeRelationshipType.LocalReachReference, ReviewStatus = UsgsGaugeLinkageReviewStatus.Provisional, Notes = "Local Tar River reach reference near Louisburg." }
    ];

    public List<River> GetPresetRivers()
    {
        return PresetRivers
            .Select(river => new River
            {
                Id = river.Id,
                Name = river.Name,
                Region = river.Region,
                State = river.State,
                Notes = river.Notes
            })
            .ToList();
    }

    public List<AccessPoint> GetPresetAccessPoints()
    {
        return PresetAccessPoints
            .Select(accessPoint => new AccessPoint
            {
                Id = accessPoint.Id,
                RiverId = accessPoint.RiverId,
                Name = accessPoint.Name,
                AccessType = accessPoint.AccessType,
                RiverOrder = accessPoint.RiverOrder,
                IsPublic = accessPoint.IsPublic,
                RiverMile = accessPoint.RiverMile,
                RiverMileSource = GetAccessPointRiverMileSource(accessPoint),
                RiverMileConfidence = GetAccessPointRiverMileConfidence(accessPoint),
                LastReviewedAt = RiverMileSeedReviewDate,
                ReviewTrigger = RiverMileReviewTrigger,
                Address = accessPoint.Address,
                Amenities = accessPoint.Amenities,
                SourceName = accessPoint.SourceName,
                Latitude = accessPoint.Latitude,
                Longitude = accessPoint.Longitude,
                Notes = accessPoint.Notes
            })
            .ToList();
    }

    public List<UsgsGauge> GetPresetUsgsGauges()
    {
        return PresetUsgsGauges
            .Select(gauge => new UsgsGauge
            {
                Id = gauge.Id,
                RiverId = gauge.RiverId,
                StationId = gauge.StationId,
                Name = gauge.Name,
                Source = gauge.Source,
                SourceReference = gauge.SourceReference,
                RiverMile = gauge.RiverMile,
                RiverMileSource = gauge.RiverMileSource ?? UsgsGaugeRiverMileSource,
                RiverMileConfidence = gauge.RiverMileConfidence ?? UsgsGaugeRiverMileConfidence,
                LastReviewedAt = gauge.LastReviewedAt ?? RiverMileSeedReviewDate,
                ReviewTrigger = gauge.ReviewTrigger ?? RiverMileReviewTrigger,
                Notes = gauge.Notes
            })
            .ToList();
    }

    public List<UsgsGaugeImportTarget> GetPresetUsgsGaugeImportTargets()
    {
        return PresetUsgsGaugeImportTargets
            .Select(target => new UsgsGaugeImportTarget
            {
                GaugeId = target.GaugeId,
                SegmentId = target.SegmentId,
                RelationshipType = target.RelationshipType,
                ReviewStatus = target.ReviewStatus,
                MappingConfidence = target.MappingConfidence ?? ProvisionalGaugeMappingConfidence,
                MappingConfidenceSource = target.MappingConfidenceSource ?? ProvisionalGaugeMappingConfidenceSource,
                Notes = target.Notes
            })
            .ToList();
    }

    public List<SegmentRiverMileDistanceComparison> GetRiverMileDistanceComparisons()
    {
        return GetPresetSegments()
            .Select(segment =>
            {
                var delta = segment.RiverMileDistanceMiles.HasValue
                    ? Math.Round(segment.RiverMileDistanceMiles.Value - segment.DistanceMiles, 2)
                    : (double?)null;

                return new SegmentRiverMileDistanceComparison
                {
                    SegmentId = segment.Id,
                    ExistingDistance = segment.DistanceMiles,
                    RiverMileDistance = segment.RiverMileDistanceMiles,
                    Delta = delta,
                    ReviewClassification = ClassifyDistanceComparison(delta)
                };
            })
            .ToList();
    }

    public List<Segment> GetPresetSegments()
    {
        var accessPointsById = PresetAccessPoints.ToDictionary(accessPoint => accessPoint.Id);

        return PresetSegments
            .Select(segment =>
            {
                accessPointsById.TryGetValue(segment.StartAccessPointId, out var startAccess);
                accessPointsById.TryGetValue(segment.EndAccessPointId, out var endAccess);

                return new Segment
                {
                    Id = segment.Id,
                    RiverId = segment.RiverId,
                    StartAccessPointId = segment.StartAccessPointId,
                    EndAccessPointId = segment.EndAccessPointId,
                    RiverOrder = segment.RiverOrder,
                    Name = segment.Name,
                    PutInName = segment.PutInName,
                    TakeOutName = segment.TakeOutName,
                    DistanceMiles = segment.DistanceMiles,
                    DistanceSource = segment.DistanceSource,
                    PutInRiverMile = startAccess?.RiverMile,
                    TakeOutRiverMile = endAccess?.RiverMile,
                    RiverMileDistanceMiles = CalculateRiverMileDistance(startAccess, endAccess),
                    PutInAddress = startAccess?.Address,
                    TakeOutAddress = endAccess?.Address,
                    PutInAmenities = startAccess?.Amenities,
                    TakeOutAmenities = endAccess?.Amenities,
                    PlanningSource = GetPlanningSource(segment, startAccess, endAccess),
                    PutInLatitude = segment.PutInLatitude ?? startAccess?.Latitude,
                    PutInLongitude = segment.PutInLongitude ?? startAccess?.Longitude,
                    TakeOutLatitude = segment.TakeOutLatitude ?? endAccess?.Latitude,
                    TakeOutLongitude = segment.TakeOutLongitude ?? endAccess?.Longitude,
                    DefaultCurrentMph = segment.DefaultCurrentMph,
                    IsActive = segment.IsActive
                };
            })
            .ToList();
    }

    public Segment? GetSegment(Guid segmentId)
    {
        return GetPresetSegments().FirstOrDefault(segment => segment.Id == segmentId);
    }

    private static string? GetPlanningSource(Segment segment, AccessPoint? startAccess, AccessPoint? endAccess)
    {
        if (!string.IsNullOrWhiteSpace(segment.DistanceSource))
            return segment.DistanceSource;

        if (!string.IsNullOrWhiteSpace(startAccess?.SourceName) &&
            string.Equals(startAccess.SourceName, endAccess?.SourceName, StringComparison.Ordinal))
            return startAccess.SourceName;

        return null;
    }

    private static double? CalculateRiverMileDistance(AccessPoint? startAccess, AccessPoint? endAccess)
    {
        var startRiverMile = startAccess?.RiverMile;
        var endRiverMile = endAccess?.RiverMile;

        if (!startRiverMile.HasValue || !endRiverMile.HasValue)
            return null;

        return Math.Round(Math.Abs(endRiverMile.Value - startRiverMile.Value), 2);
    }

    private static DistanceReconciliationReviewClassification ClassifyDistanceComparison(double? delta)
    {
        if (!delta.HasValue)
            return DistanceReconciliationReviewClassification.RequiresReview;

        if (delta.Value == 0)
            return DistanceReconciliationReviewClassification.Aligned;

        return DistanceReconciliationReviewClassification.RequiresReview;
    }

    private static string? GetAccessPointRiverMileSource(AccessPoint accessPoint)
    {
        if (!string.IsNullOrWhiteSpace(accessPoint.RiverMileSource))
            return accessPoint.RiverMileSource;

        if (!string.IsNullOrWhiteSpace(accessPoint.SourceName))
            return accessPoint.SourceName;

        return LegacyCatalogRiverMileSource;
    }

    private static double? GetAccessPointRiverMileConfidence(AccessPoint accessPoint)
    {
        if (accessPoint.RiverMileConfidence.HasValue)
            return accessPoint.RiverMileConfidence;

        if (!string.IsNullOrWhiteSpace(accessPoint.SourceName))
            return RaleighNeuseRiverMileConfidence;

        return LegacyCatalogRiverMileConfidence;
    }
}
