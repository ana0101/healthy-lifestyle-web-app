﻿using AutoMapper;
using healthy_lifestyle_web_app.ContextModels;
using healthy_lifestyle_web_app.Entities;
using healthy_lifestyle_web_app.Models;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace healthy_lifestyle_web_app.Repositories
{
    public class DayRepository: IDayRepository
    {
        private readonly ApplicationContext _context;
        private readonly IWeightEvolutionRepository _weightEvolutionRepository;

        public DayRepository(ApplicationContext context, IWeightEvolutionRepository weightEvolutionRepository)
        {
            _context = context;
            _weightEvolutionRepository = weightEvolutionRepository;
        }   

        public async Task<List<Day>> GetAllAsync()
        {
            return await _context.Days.Include(d => d.DayPhysicalActivities)
                .Include(d => d.DayFoods).ToListAsync();
        }

        // Get by profileId
        public async Task<List<Day>> GetByUserAsync(int id)
        { 
            return await _context.Days.Where(d => d.ProfileId == id)
                .Include(d => d.DayPhysicalActivities)
                .Include(d => d.DayFoods).ToListAsync();
        }

        public async Task<List<Day>> GetAfterDateAsync(int profileId, DateOnly date)
        {
            return await _context.Days.Where(d => d.ProfileId == profileId && d.Date >= date)
                .Include(d => d.DayPhysicalActivities)
                .Include(d => d.DayFoods).ToListAsync();
        }

        public async Task<Day?> GetCurrentDayAsync(int id)
        {
            List<Day> days =  await _context.Days.Where(d => d.ProfileId == id)
                .Include(d => d.DayPhysicalActivities).Include(d => d.DayFoods).ToListAsync();

            DateOnly currentDay = DateOnly.FromDateTime(DateTime.Today);

            return days.FirstOrDefault(d => d.Date == currentDay);
        }

        public async Task<Day?> GetByDateAsync(int id, DateOnly date)
        {
            return await _context.Days
                .Include(d => d.DayFoods).Include(d => d.DayPhysicalActivities)
                .FirstOrDefaultAsync(d => d.ProfileId == id && d.Date == date);
        }

        public async Task<double> GetFoodCalories(int id, DateOnly date)
        {
            List<DayFood> dayFoods = await _context.DayFoods.Include(df => df.Food)
                .Where(df => df.ProfileId == id && df.Date == date).ToListAsync();

            double calories = 0;
            foreach(DayFood dayFood in dayFoods)
            {
                calories += (dayFood.Food.Calories * dayFood.Grams / 100);
            }
            return calories;
        }

        public async Task<double> GetActivityCalories(int profileId, DateOnly date)
        {
            List<DayPhysicalActivity> dayActivities = await _context.DayPhysicalActivities
                .Include(dpa => dpa.PhysicalActivity)
                .Where(dpa => dpa.ProfileId == profileId && dpa.Date == date)
                .ToListAsync();

            double calories = 0;
            foreach (DayPhysicalActivity dayActivity in dayActivities)
            {
                calories += (dayActivity.PhysicalActivity.Calories * dayActivity.Minutes);
            }
            return calories;
        }

        public async Task<List<DateCaloriesModel>> GetDaysFoodCaloriesAsync(List<Day> days)
        {
            List<DateCaloriesModel> list = new List<DateCaloriesModel>();
            foreach (Day day in days)
            {
                double calories = await GetFoodCalories(day.ProfileId, day.Date);
                DateCaloriesModel dayFoodCaloriesModel = new DateCaloriesModel(day.Date, calories);
                list.Add(dayFoodCaloriesModel);
            }
            return list;
        }

        public async Task<List<DateCaloriesModel>> GetDaysActivityCaloriesAsync(List<Day> days)
        {
            List<DateCaloriesModel> list = new List<DateCaloriesModel>();
            foreach (Day day in days)
            {
                double calories = await GetActivityCalories(day.ProfileId, day.Date);
                DateCaloriesModel dateCaloriesModel = new DateCaloriesModel(day.Date, calories);
                list.Add(dateCaloriesModel);
            }
            return list;
        }

        public async Task<double> GetAverageCalories(List<DateCaloriesModel> datesCalories)
        {
            double calories = 0;
            foreach (DateCaloriesModel dateCalories in datesCalories)
            {
                calories += dateCalories.Calories;
            }
            return calories / datesCalories.Count;
        }

        public async Task<bool> PostDayAsync(Entities.Profile profile)
        {
            Goal goal = profile.Goal;
            double we = profile.Weight;

            int calories = (int)(we * 38);

            if (goal == Goal.Lose)
            {
                calories = (int)(0.8 * calories);
            } else if (goal == Goal.Gain) {
                calories = (int)(1.2 * calories);
            }

            Day day = new(profile.Id, DateOnly.FromDateTime(DateTime.Today), calories);
            
            try
            {
                _context.Days.Add(day);
                await _context.SaveChangesAsync();
            }
            catch (DbException)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> PutFoodAsync(Day day, Food food, int grams)
        {
            try
            {
                _context.DayFoods.Add(new(day.ProfileId, day.Date, food.Id, grams));
                await _context.SaveChangesAsync();
            }
            catch (DbException)
            {
                return false;
            }
            return true;
        }

        public async Task<bool> PutPhysicalActivityAsync(Day day, PhysicalActivity activity, int minutes)
        {
            try
            {
                _context.DayPhysicalActivities.Add(new(day.ProfileId, day.Date, activity.Id, minutes));
                await _context.SaveChangesAsync();
            }
            catch (DbException)
            {
                return false;
            }
            return true;
        }

        public async Task<bool> UpdateGramsAsync(Day day, int foodId, int grams)
        {
            DayFood? dayfood = day.DayFoods.FirstOrDefault(f => f.FoodId == foodId);
            if(dayfood == null)
            {
                return false;
            }

            try
            {
                dayfood.Grams = grams;
                await _context.SaveChangesAsync();
            }
            catch(DbException)
            {
                return false;
            }
            
            return true;
        }

        public async Task<bool> UpdateMinutesAsync(Day day, int activityId, int minutes)
        {
            DayPhysicalActivity? activity = day.DayPhysicalActivities
                                .FirstOrDefault(f => f.PhysicalActivityId == activityId);
            if (activity == null)
            {
                return false;
            }

            try
            {
                activity.Minutes = minutes;
                await _context.SaveChangesAsync();
            }
            catch (DbException)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteFoodAsync(Day day, int foodId)
        {
            DayFood? dayFood = day.DayFoods.FirstOrDefault(d => d.FoodId == foodId);
            if(dayFood == null)
            {  
                return false; 
            }

            try
            {
                day.DayFoods.Remove(dayFood);
                await _context.SaveChangesAsync();
            }
            catch(DbException)
            {
                return false;
            }
            return true;
        }

        public async Task<bool> DeletePhysicalActivityAsync(Day day, int activityId)
        {
            DayPhysicalActivity? dayPhysicalActivity = day.DayPhysicalActivities
                                .FirstOrDefault(d => d.PhysicalActivityId == activityId);
            if (dayPhysicalActivity == null)
            {
                return false;
            }

            try
            {
                day.DayPhysicalActivities.Remove(dayPhysicalActivity);
                await _context.SaveChangesAsync();
            }
            catch (DbException)
            {
                return false;
            }
            return true;
        }
    }
}
