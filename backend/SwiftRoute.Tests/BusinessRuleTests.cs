using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SwiftRoute.API.DTOs;
using SwiftRoute.API.Hubs;
using SwiftRoute.API.Models;
using SwiftRoute.API.Repositories;
using SwiftRoute.API.Services;
using Xunit;

namespace SwiftRoute.Tests;

/// <summary>
/// Unit tests for AssignmentService business rules.
/// All dependencies are mocked — no database required.
/// </summary>
public class AssignmentServiceTests
{
    // ── Shared test fixtures ──────────────────────────────────────────────────
    private readonly Mock<IUnitOfWork>         _uow  = new();
    private readonly Mock<IDriverRepository>   _drv  = new();
    private readonly Mock<IVehicleRepository>  _veh  = new();
    private readonly Mock<IAssignmentRepository> _asgn = new();
    private readonly Mock<IHubContext<PulseHub>> _hub  = new();
    private readonly Mock<IHubClients>           _hubClients = new();
    private readonly Mock<IClientProxy>          _clientProxy = new();

    private AssignmentService CreateService()
    {
        _uow.SetupGet(u => u.Drivers).Returns(_drv.Object);
        _uow.SetupGet(u => u.Vehicles).Returns(_veh.Object);
        _uow.SetupGet(u => u.Assignments).Returns(_asgn.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        _hub.SetupGet(h => h.Clients).Returns(_hubClients.Object);
        _hubClients.Setup(c => c.All).Returns(_clientProxy.Object);
        _clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                    .Returns(Task.CompletedTask);

        return new AssignmentService(_uow.Object, _hub.Object);
    }

    private static Driver ValidDriver() => new()
    {
        Id = "D001", Name = "Rajesh Kumar", LicenseClass = "ClassA",
        LicenseExpiry = DateTime.UtcNow.AddYears(1), Status = DriverStatus.Available
    };

    private static Driver ExpiredDriver() => new()
    {
        Id = "D002", Name = "Amit Sharma", LicenseClass = "ClassB",
        LicenseExpiry = new DateTime(2023, 1, 15), Status = DriverStatus.Available
    };

    private static Vehicle ActiveVehicleClassA() => new()
    {
        VIN = "V101", Model = "BharatBenz 3523R", LicenseClass = "ClassA",
        CurrentOdometer = 45000, LastServiceOdometer = 42000, Status = VehicleStatus.Active
    };

    private static CreateAssignmentRequest ValidRequest(string driverId = "D001", string vin = "V101") => new(
        driverId, vin, "Mumbai–Pune Route", "Mumbai", "Pune",
        DateTime.UtcNow.AddHours(1), null
    );

    // ══════════════════════════════════════════════════════════════════════════
    // UC-01: License Validity
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAssignment_ExpiredLicense_ThrowsException()
    {
        // Arrange
        var svc = CreateService();
        _drv.Setup(r => r.GetByIdAsync("D002")).ReturnsAsync(ExpiredDriver());

        // Act
        var act = async () => await svc.CreateAssignmentAsync(ValidRequest("D002"), "U001");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired license*");
    }

    [Fact]
    public async Task CreateAssignment_ValidLicense_ProceedsPastLicenseCheck()
    {
        // Arrange
        var svc    = CreateService();
        var driver = ValidDriver();
        var vehicle = ActiveVehicleClassA();
        _drv.Setup(r => r.GetByIdAsync("D001")).ReturnsAsync(driver);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _asgn.Setup(r => r.IsDriverActivelyAssignedAsync("D001")).ReturnsAsync(false);
        _asgn.Setup(r => r.IsVehicleActivelyAssignedAsync("V101")).ReturnsAsync(false);
        _asgn.Setup(r => r.CreateAsync(It.IsAny<Assignment>())).ReturnsAsync(new Assignment
            { Id = 1, DriverId = "D001", VehicleVIN = "V101", Route = "Test",
              Origin = "A", Destination = "B", ScheduledStart = DateTime.UtcNow, Status = AssignmentStatus.Pending });
        _drv.Setup(r => r.UpdateAsync(It.IsAny<Driver>())).ReturnsAsync(driver);
        _veh.Setup(r => r.UpdateAsync(It.IsAny<Vehicle>())).ReturnsAsync(vehicle);

        // Act — should not throw
        var result = await svc.CreateAssignmentAsync(ValidRequest(), "U001");

        // Assert
        result.Should().NotBeNull();
        result.DriverId.Should().Be("D001");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UC-02: Vehicle Status Lock
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(VehicleStatus.MaintenanceRequired)]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.OutOfService)]
    [InlineData(VehicleStatus.InTransit)]
    public async Task CreateAssignment_NonActiveVehicle_ThrowsException(VehicleStatus status)
    {
        // Arrange
        var svc = CreateService();
        var driver = ValidDriver();
        var vehicle = ActiveVehicleClassA();
        vehicle.Status = status;

        _drv.Setup(r => r.GetByIdAsync("D001")).ReturnsAsync(driver);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);

        // Act
        var act = async () => await svc.CreateAssignmentAsync(ValidRequest(), "U001");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public async Task CreateAssignment_ActiveVehicle_AllowsAssignment()
    {
        // Arrange — same setup as ValidLicense test
        var svc    = CreateService();
        var driver = ValidDriver();
        var vehicle = ActiveVehicleClassA(); // Status = Active
        _drv.Setup(r => r.GetByIdAsync("D001")).ReturnsAsync(driver);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _asgn.Setup(r => r.IsDriverActivelyAssignedAsync("D001")).ReturnsAsync(false);
        _asgn.Setup(r => r.IsVehicleActivelyAssignedAsync("V101")).ReturnsAsync(false);
        _asgn.Setup(r => r.CreateAsync(It.IsAny<Assignment>())).ReturnsAsync(new Assignment
            { Id = 1, DriverId = "D001", VehicleVIN = "V101", Route = "T",
              Origin = "A", Destination = "B", ScheduledStart = DateTime.UtcNow, Status = AssignmentStatus.Pending });
        _drv.Setup(r => r.UpdateAsync(It.IsAny<Driver>())).ReturnsAsync(driver);
        _veh.Setup(r => r.UpdateAsync(It.IsAny<Vehicle>())).ReturnsAsync(vehicle);

        // Act — should not throw
        Func<Task> act = async () => await svc.CreateAssignmentAsync(ValidRequest(), "U001");
        await act.Should().NotThrowAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UC-03: Resource Exclusivity
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAssignment_DriverAlreadyAssigned_ThrowsException()
    {
        // Arrange
        var svc = CreateService();
        var driver  = ValidDriver();
        var vehicle = ActiveVehicleClassA();
        _drv.Setup(r => r.GetByIdAsync("D001")).ReturnsAsync(driver);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _asgn.Setup(r => r.IsDriverActivelyAssignedAsync("D001")).ReturnsAsync(true); // ← driver busy

        // Act
        var act = async () => await svc.CreateAssignmentAsync(ValidRequest(), "U001");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already on an active assignment*");
    }

    [Fact]
    public async Task CreateAssignment_VehicleAlreadyAssigned_ThrowsException()
    {
        // Arrange
        var svc = CreateService();
        var driver  = ValidDriver();
        var vehicle = ActiveVehicleClassA();
        _drv.Setup(r => r.GetByIdAsync("D001")).ReturnsAsync(driver);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _asgn.Setup(r => r.IsDriverActivelyAssignedAsync("D001")).ReturnsAsync(false);
        _asgn.Setup(r => r.IsVehicleActivelyAssignedAsync("V101")).ReturnsAsync(true); // ← vehicle busy

        // Act
        var act = async () => await svc.CreateAssignmentAsync(ValidRequest(), "U001");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already assigned*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UC-04: License Class Matching
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAssignment_ClassBDriverClassAVehicle_ThrowsException()
    {
        // Arrange
        var svc = CreateService();
        var classBDriver = new Driver
        {
            Id = "D005", Name = "Kavitha Reddy", LicenseClass = "ClassB",
            LicenseExpiry = DateTime.UtcNow.AddYears(1), Status = DriverStatus.Available
        };
        var classAVehicle = ActiveVehicleClassA(); // ClassA vehicle

        _drv.Setup(r => r.GetByIdAsync("D005")).ReturnsAsync(classBDriver);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(classAVehicle);

        // Act
        var act = async () => await svc.CreateAssignmentAsync(ValidRequest("D005"), "U001");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Class B*Class A*");
    }

    [Fact]
    public async Task CreateAssignment_ClassADriverClassBVehicle_IsAllowed()
    {
        // A Class A driver CAN drive a Class B vehicle (upward compatibility)
        var svc = CreateService();
        var driver = ValidDriver(); // ClassA
        var classBVehicle = new Vehicle
        {
            VIN = "V104", Model = "Mahindra Blazo", LicenseClass = "ClassB",
            CurrentOdometer = 23000, LastServiceOdometer = 22000, Status = VehicleStatus.Active
        };
        _drv.Setup(r => r.GetByIdAsync("D001")).ReturnsAsync(driver);
        _veh.Setup(r => r.GetByVINAsync("V104")).ReturnsAsync(classBVehicle);
        _asgn.Setup(r => r.IsDriverActivelyAssignedAsync("D001")).ReturnsAsync(false);
        _asgn.Setup(r => r.IsVehicleActivelyAssignedAsync("V104")).ReturnsAsync(false);
        _asgn.Setup(r => r.CreateAsync(It.IsAny<Assignment>())).ReturnsAsync(new Assignment
            { Id = 2, DriverId = "D001", VehicleVIN = "V104", Route = "T",
              Origin = "A", Destination = "B", ScheduledStart = DateTime.UtcNow, Status = AssignmentStatus.Pending });
        _drv.Setup(r => r.UpdateAsync(It.IsAny<Driver>())).ReturnsAsync(driver);
        _veh.Setup(r => r.UpdateAsync(It.IsAny<Vehicle>())).ReturnsAsync(classBVehicle);

        Func<Task> act = async () => await svc.CreateAssignmentAsync(ValidRequest("D001", "V104"), "U001");
        await act.Should().NotThrowAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UC-05: Driver and Vehicle Not Found
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateAssignment_DriverNotFound_ThrowsException()
    {
        var svc = CreateService();
        _drv.Setup(r => r.GetByIdAsync("DXXX")).ReturnsAsync((Driver?)null);

        var act = async () => await svc.CreateAssignmentAsync(ValidRequest("DXXX"), "U001");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateAssignment_VehicleNotFound_ThrowsException()
    {
        var svc = CreateService();
        _drv.Setup(r => r.GetByIdAsync("D001")).ReturnsAsync(ValidDriver());
        _veh.Setup(r => r.GetByVINAsync("VXXX")).ReturnsAsync((Vehicle?)null);

        var act = async () => await svc.CreateAssignmentAsync(ValidRequest("D001", "VXXX"), "U001");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}

/// <summary>
/// Unit tests for VehicleService — especially the 10k km maintenance threshold rule.
/// </summary>
public class VehicleServiceTests
{
    private readonly Mock<IUnitOfWork>        _uow = new();
    private readonly Mock<IVehicleRepository> _veh = new();
    private readonly Mock<IHubContext<PulseHub>> _hub = new();
    private readonly Mock<IHubClients>        _hubClients  = new();
    private readonly Mock<IClientProxy>       _clientProxy = new();

    private VehicleService CreateService()
    {
        _uow.SetupGet(u => u.Vehicles).Returns(_veh.Object);
        _uow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _hub.SetupGet(h => h.Clients).Returns(_hubClients.Object);
        _hubClients.Setup(c => c.All).Returns(_clientProxy.Object);
        _clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                    .Returns(Task.CompletedTask);
        return new VehicleService(_uow.Object, _hub.Object);
    }

    private static Vehicle ActiveVehicle(double current = 45000, double lastService = 42000) => new()
    {
        VIN = "V101", Model = "BharatBenz 3523R", LicenseClass = "ClassA",
        CurrentOdometer = current, LastServiceOdometer = lastService, Status = VehicleStatus.Active
    };

    // ══════════════════════════════════════════════════════════════════════════
    // UC-06: 10,000 km Maintenance Threshold
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateOdometer_CrossesThreshold_SetsMaintenanceRequired()
    {
        // Arrange: lastService=42000, new reading=52001 → diff=10001 → flag
        var svc     = CreateService();
        var vehicle = ActiveVehicle(45000, 42000);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _veh.Setup(r => r.UpdateAsync(It.IsAny<Vehicle>())).ReturnsAsync(vehicle);

        // Act
        var result = await svc.UpdateOdometerAsync("V101", 52001);

        // Assert
        result.Status.Should().Be("MaintenanceRequired");
        result.NeedsService.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateOdometer_CrossesThreshold_BroadcastsSignalRAlert()
    {
        var svc     = CreateService();
        var vehicle = ActiveVehicle(45000, 42000);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _veh.Setup(r => r.UpdateAsync(It.IsAny<Vehicle>())).ReturnsAsync(vehicle);

        await svc.UpdateOdometerAsync("V101", 52001);

        // Verify SignalR MaintenanceAlert was sent
        _clientProxy.Verify(c =>
            c.SendCoreAsync("MaintenanceAlert", It.IsAny<object[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task UpdateOdometer_BelowThreshold_StatusRemainsActive()
    {
        var svc     = CreateService();
        var vehicle = ActiveVehicle(45000, 42000);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _veh.Setup(r => r.UpdateAsync(It.IsAny<Vehicle>())).ReturnsAsync(vehicle);

        var result = await svc.UpdateOdometerAsync("V101", 49999); // diff = 7999

        result.Status.Should().Be("Active");
        result.NeedsService.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateOdometer_ExactlyAtThreshold_SetsMaintenanceRequired()
    {
        var svc     = CreateService();
        var vehicle = ActiveVehicle(45000, 42000);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);
        _veh.Setup(r => r.UpdateAsync(It.IsAny<Vehicle>())).ReturnsAsync(vehicle);

        var result = await svc.UpdateOdometerAsync("V101", 52000); // diff = exactly 10000

        result.Status.Should().Be("MaintenanceRequired");
    }

    [Fact]
    public async Task UpdateOdometer_Decreasing_ThrowsException()
    {
        var svc     = CreateService();
        var vehicle = ActiveVehicle(45000, 42000);
        _veh.Setup(r => r.GetByVINAsync("V101")).ReturnsAsync(vehicle);

        var act = async () => await svc.UpdateOdometerAsync("V101", 44000);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be decreased*");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UC-07: Driver.IsLicenseValid computed property
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Driver_ExpiredLicense_IsLicenseValidReturnsFalse()
    {
        var driver = new Driver { LicenseExpiry = DateTime.UtcNow.AddDays(-1) };
        driver.IsLicenseValid.Should().BeFalse();
    }

    [Fact]
    public void Driver_FutureLicense_IsLicenseValidReturnsTrue()
    {
        var driver = new Driver { LicenseExpiry = DateTime.UtcNow.AddDays(1) };
        driver.IsLicenseValid.Should().BeTrue();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UC-08: Vehicle.NeedsService computed property
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(42000, 42000, false)]  // 0 km since service
    [InlineData(51999, 42000, false)]  // 9999 km since service
    [InlineData(52000, 42000, true)]   // exactly 10000 km
    [InlineData(55000, 42000, true)]   // 13000 km since service
    public void Vehicle_NeedsService_CorrectThreshold(double current, double lastService, bool expected)
    {
        var v = new Vehicle { CurrentOdometer = current, LastServiceOdometer = lastService };
        v.NeedsService.Should().Be(expected);
    }
}
