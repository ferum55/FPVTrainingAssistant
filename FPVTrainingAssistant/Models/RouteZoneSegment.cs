namespace FPVTrainingAssistant.Models;

// Presentation geometry only. RedRadiusM is not an analytical scoring boundary.
public sealed record RouteZoneSegment(Point2D Start, Point2D End, double GreenRadiusM, double YellowRadiusM, double RedRadiusM);
