using HospitalManagementSystem.Domain.Entities;
using HospitalManagementSystem.Domain.Repositories;
using HospitalManagementSystem.Application.DTOs.Appointment;
using HospitalManagementSystem.Application.DTOs.Common;
using HospitalManagementSystem.Domain.Specifications;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore; // Required for ToListAsync, CountAsync

namespace HospitalManagementSystem.Application.Services
{
    public class AppointmentApplicationService
    {
        private readonly IAppointmentRepository _appointmentRepository;

        public AppointmentApplicationService(IAppointmentRepository appointmentRepository)
        {
            _appointmentRepository = appointmentRepository;
        }

        public async Task<PagedResult<Appointment>> GetAppointmentsForPatientAsync(int patientId, AppointmentFilterDto filter, int page, int pageSize)
        {
            ISpecification<Appointment> spec = new AppointmentForPatientSpecification(patientId);

            if (!string.IsNullOrEmpty(filter.Status))
            {
                spec = spec.And(new AppointmentByStatusSpecification(filter.Status));
            }

            if (filter.StartDate.HasValue && filter.EndDate.HasValue)
            {
                // FIX: Explicitly set Kind to UTC for PostgreSQL
                var startDateUtc = DateTime.SpecifyKind(filter.StartDate.Value.Date, DateTimeKind.Utc);
                var endDateValue = filter.EndDate.Value.Date.AddDays(1).AddTicks(-1);
                var endDateUtc = DateTime.SpecifyKind(endDateValue, DateTimeKind.Utc);

                spec = spec.And(new AppointmentByDateRangeSpecification(startDateUtc, endDateUtc));
            }

            var query = _appointmentRepository.GetQueryable(spec);
            var totalCount = await query.CountAsync();

            var appointments = await query
                .OrderByDescending(a => a.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<Appointment>(appointments, page, pageSize, totalCount);
        }
    }

    // Restoring original structure for tidiness
    public static class SpecificationExtensions
    {
        public static ISpecification<T> And<T>(this ISpecification<T> left, ISpecification<T> right)
        {
            return new AndSpecification<T>(left, right);
        }
    }

    public class AndSpecification<T> : BaseSpecification<T>
    {
        public AndSpecification(ISpecification<T> left, ISpecification<T> right)
            : base(CombineExpressions(left.Criteria, right.Criteria))
        {
            left.Includes.ForEach(AddInclude);
            right.Includes.ForEach(AddInclude);
            left.IncludeStrings.ForEach(AddInclude);
            right.IncludeStrings.ForEach(AddInclude);
        }

        private static Expression<Func<T, bool>> CombineExpressions(Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(T));
            var leftVisitor = new ReplaceExpressionVisitor(left.Parameters[0], parameter);
            var leftBody = leftVisitor.Visit(left.Body);
            var rightVisitor = new ReplaceExpressionVisitor(right.Parameters[0], parameter);
            var rightBody = rightVisitor.Visit(right.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), parameter);
        }

        private class ReplaceExpressionVisitor : ExpressionVisitor
        {
            private readonly Expression _oldValue;
            private readonly Expression _newValue;

            public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
            {
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override Expression Visit(Expression? node) => node == _oldValue ? _newValue : base.Visit(node);
        }
    }
}
