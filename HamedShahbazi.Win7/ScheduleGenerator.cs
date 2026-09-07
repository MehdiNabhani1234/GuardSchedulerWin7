using System;
using System.Collections.Generic;
using System.Linq;

namespace HamedShahbazi.Win7
{
    public class ScheduleGenerator
    {
        private readonly Random random = new Random();

        public List<GuardScheduleItem> Generate(
            int days,
            DateTime startDate,
            List<Officer> guardOfficers,
            List<Officer> reserveOfficers,
            List<Officer> chiefOfficers)
        {
            if (days <= 0)
                throw new ArgumentException("تعداد روزهای برنامه باید بیشتر از صفر باشد.");

            if (guardOfficers == null || guardOfficers.Count == 0)
                throw new InvalidOperationException("لیست افسران پاسدار خالی است.");

            if (reserveOfficers == null || reserveOfficers.Count == 0)
                throw new InvalidOperationException("لیست افسران جانشین خالی است.");

            if (chiefOfficers == null || chiefOfficers.Count == 0)
                throw new InvalidOperationException("لیست افسران سر خالی است.");

            int maxOfficerCount = Math.Max(
                guardOfficers.Count,
                Math.Max(
                    reserveOfficers.Count,
                    chiefOfficers.Count));

            if (days < maxOfficerCount)
            {
                throw new InvalidOperationException(
                    "تعداد روزهای انتخاب شده کافی نیست.\r\n\r\n" +
                    "هر افسر باید حداقل یک پاس داشته باشد.\r\n" +
                    "تعداد روز باید حداقل برابر بیشترین تعداد نیرو در یک گروه باشد.\r\n\r\n" +
                    "حداقل روز مورد نیاز: " + maxOfficerCount);
            }

            List<GuardScheduleItem> schedule =
                new List<GuardScheduleItem>();

            List<Officer> randomGuard =
                Shuffle(guardOfficers);

            List<Officer> randomReserve =
                Shuffle(reserveOfficers);

            List<Officer> randomChief =
                Shuffle(chiefOfficers);

            for (int day = 1; day <= days; day++)
            {
                DateTime programDate =
                    startDate.Date.AddDays(day - 1);

                List<Officer> usedToday =
                    new List<Officer>();

                // ============================
                // پاس 1 = افسر جانشین
                // ============================

                Officer reserveOfficer =
                    FindOfficerForDay(
                        randomReserve,
                        programDate,
                        usedToday,
                        schedule,
                        startDate);

                if (reserveOfficer != null)
                    usedToday.Add(reserveOfficer);

                schedule.Add(
                    new GuardScheduleItem
                    {
                        Day = day,
                        Pass = 1,
                        Reserve = reserveOfficer,
                        Chief = null,
                        Guard = null
                    });

                // ============================
                // پاس 2 = افسر سر
                // ============================

                Officer chiefOfficer =
                    FindOfficerForDay(
                        randomChief,
                        programDate,
                        usedToday,
                        schedule,
                        startDate);

                if (chiefOfficer != null)
                    usedToday.Add(chiefOfficer);

                schedule.Add(
                    new GuardScheduleItem
                    {
                        Day = day,
                        Pass = 2,
                        Reserve = null,
                        Chief = chiefOfficer,
                        Guard = null
                    });

                // ============================
                // پاس 3 = افسر پاسدار
                // ============================

                Officer guardOfficer =
                    FindOfficerForDay(
                        randomGuard,
                        programDate,
                        usedToday,
                        schedule,
                        startDate);

                if (guardOfficer != null)
                    usedToday.Add(guardOfficer);

                schedule.Add(
                    new GuardScheduleItem
                    {
                        Day = day,
                        Pass = 3,
                        Reserve = null,
                        Chief = null,
                        Guard = guardOfficer
                    });

                // ============================
                // بررسی اینکه هر سه پاس پر شده
                // ============================

                if (reserveOfficer == null ||
                    chiefOfficer == null ||
                    guardOfficer == null)
                {
                    throw new InvalidOperationException(
                        "برای روز " + day +
                        " امکان انتخاب نیروی مناسب وجود ندارد.\r\n\r\n" +
                        "احتمالاً ترکیب امتیازات نیروها برای این بازه کافی نیست.");
                }
            }

            return schedule;
        }

        private List<Officer> Shuffle(List<Officer> source)
        {
            return source
                .OrderBy(x => random.Next())
                .ToList();
        }

        private bool IsScoreAllowedForDay(
            double score,
            DateTime date)
        {
            DayOfWeek day = date.DayOfWeek;

            // 0.5 فقط پنجشنبه و جمعه
            if (score == 0.5)
            {
                return day == DayOfWeek.Thursday ||
                       day == DayOfWeek.Friday;
            }

            // 1 فقط شنبه، یکشنبه و دوشنبه
            if (score == 1)
            {
                return day == DayOfWeek.Saturday ||
                       day == DayOfWeek.Sunday ||
                       day == DayOfWeek.Monday;
            }

            // 2 فقط سه شنبه و چهارشنبه
            if (score == 2)
            {
                return day == DayOfWeek.Tuesday ||
                       day == DayOfWeek.Wednesday;
            }

            return false;
        }

        private bool CanUseHalfScoreOfficer(
            Officer officer,
            DateTime date,
            List<GuardScheduleItem> schedule,
            DateTime startDate)
        {
            if (officer == null)
                return false;

            if (officer.Score != 0.5)
                return true;

            // فقط پنجشنبه و جمعه
            if (date.DayOfWeek != DayOfWeek.Thursday &&
                date.DayOfWeek != DayOfWeek.Friday)
            {
                return false;
            }

            // شروع هفته از شنبه
            DateTime weekStart =
                date.AddDays(-(int)date.DayOfWeek + 6);

            DateTime weekEnd =
                weekStart.AddDays(6);

            int count = 0;

            foreach (GuardScheduleItem item in schedule)
            {
                DateTime assignedDate =
                    startDate.Date.AddDays(item.Day - 1);

                bool sameOfficer =
                    item.Guard == officer ||
                    item.Reserve == officer ||
                    item.Chief == officer;

                if (sameOfficer &&
                    assignedDate >= weekStart &&
                    assignedDate <= weekEnd)
                {
                    count++;
                }
            }

            // هر افسر 0.5 فقط یک بار در هفته
            return count < 1;
        }

        private Officer FindOfficerForDay(
            List<Officer> officers,
            DateTime date,
            List<Officer> usedToday,
            List<GuardScheduleItem> schedule,
            DateTime startDate)
        {
            List<Officer> available =
                officers
                    .Where(x =>
                        x != null &&
                        !x.IsReplacement &&
                        IsScoreAllowedForDay(
                            x.Score,
                            date) &&
                        CanUseHalfScoreOfficer(
                            x,
                            date,
                            schedule,
                            startDate) &&
                        !usedToday.Contains(x))
                    .ToList();

            if (available.Count == 0)
                return null;

            var candidates =
                available
                    .Select(x => new
                    {
                        Officer = x,
                        PassCount = GetOfficerPassCount(
                            x,
                            schedule)
                    })
                    .ToList();

            int minPassCount =
                candidates.Min(x => x.PassCount);

            List<Officer> leastUsed =
                candidates
                    .Where(x => x.PassCount == minPassCount)
                    .Select(x => x.Officer)
                    .ToList();

            if (leastUsed.Count == 1)
                return leastUsed[0];

            // بین افراد هم‌پاس،
            // امتیاز کمتر را ترجیح بده
            double bestScore =
                leastUsed.Min(x => x.Score);

            List<Officer> scoreCandidates =
                leastUsed
                    .Where(x => x.Score == bestScore)
                    .ToList();

            if (scoreCandidates.Count == 1)
                return scoreCandidates[0];

            // اگر کاملاً برابر بودند،
            // تصادفی انتخاب می‌شود
            return scoreCandidates[
                random.Next(scoreCandidates.Count)];
        }

        private int GetOfficerPassCount(
            Officer officer,
            List<GuardScheduleItem> schedule)
        {
            return schedule.Count(x =>
                x.Guard == officer ||
                x.Reserve == officer ||
                x.Chief == officer);
        }
    }
}