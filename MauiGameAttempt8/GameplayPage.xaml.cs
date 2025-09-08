using Microsoft.Maui.Controls.Shapes;
using System.Drawing;
using Path = Microsoft.Maui.Controls.Shapes.Path;
using Point = Microsoft.Maui.Graphics.Point;
using PointF = Microsoft.Maui.Graphics.PointF;
using Color = Microsoft.Maui.Graphics.Color;
using System.Numerics;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Media;
using NAudio.Wave;
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
using Microsoft.UI.Xaml.Input;
using Windows;
using Windows.UI.Core;
using Windows.System;
#endif

namespace MauiGameAttempt8
{
    public partial class GameplayPage : ContentPage
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        static double mapScale = 1.0;
        static int canvasWidth;
        static int canvasHeight;
        static int fps = 60; // 60 FPS
        List<CanvasAnimation> currentAnimations = new List<CanvasAnimation>();
        bool keysEnabled = true;
        bool playerInvincible = false;
        bool dashOnCooldown = false;
        int dashCooldownDuration = 1000; // in milliseconds
        double dashCooldownLeft = 0; // in milliseconds
        SoundManager soundManager = new SoundManager();
        public GameplayPage()
        {
            InitializeComponent();
            playerSquare = new ColoredPolygon(new List<SKPoint> { new SKPoint(100, 100), new SKPoint(100, 200), new SKPoint(200, 200), new SKPoint(200, 100) }, new SKPaint { Color = new SKColor(0, 255, 255, 255) }, 0, 0, 30);

            //soundManager.LoadSound("enemy_hit", "C:\\Users\\Dudi\\source\\repos\\MauiGameAttempt8\\MauiGameAttempt8\\Resources\\Raw\\enemy_hit.wav");
            //soundManager.LoadSound("enemy_explodes", "C:\\Users\\Dudi\\source\\repos\\MauiGameAttempt8\\MauiGameAttempt8\\Resources\\Raw\\enemy_explodes.wav");
            //soundManager.LoadSound("background_music", "C:\\Users\\Dudi\\source\\repos\\MauiGameAttempt8\\MauiGameAttempt8\\Resources\\Raw\\epic_guitar_track.wav");
            //soundManager.PlayLoopingMusic("background_music");
        }
        static SKColorFilter negativeFilter = SKColorFilter.CreateColorMatrix(new float[]
        {
            -1, 0, 0, 0, 1,
            0, -1, 0, 0, 1,
            0, 0, -1, 0, 1,
            0, 0, 0, 1, 0
        });
        static SKColorFilter defaultFilter = SKColorFilter.CreateColorMatrix(new float[]
        {
            1, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, 1, 0
        });
        static SKColorFilter currentColorFilter = defaultFilter;
        static CanvasAnimation generateEnemyDeathAnimation(ColoredPolygon enemy)
        {
            List<SKSurface> frames = new List<SKSurface>();
            for (int i = 0; i < 20; i++)
            {
                var surface = SKSurface.Create(new SKImageInfo((int)(50 + i * Math.Sqrt(i) * 2), (int)(50 + i * Math.Sqrt(i) * 2)));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.White);
                frames.Add(surface);
            }
            return new CanvasAnimation(frames, enemy.center);
        }
        static Random rand = new Random();
        ColoredPolygon playerSquare;
        List<ColoredPolygon> straightLineEnemies = new List<ColoredPolygon>();
        List<ColoredPolygon> followingEnemies = new List<ColoredPolygon>();
        ////////////////////////////////////////////////////////////////////////////////////////
        SKPoint generateRandomPointOnEdgeOfMap()
        {
            SKPoint mapCenter = new SKPoint(canvasWidth / 2f, canvasHeight / 2f);
            SKPoint returnValue = new SKPoint();
            switch (rand.Next(4))
            {
                case 0:
                    returnValue = ScaleSKPoint(new SKPoint(rand.Next(canvasWidth), 0), mapCenter, mapScale);
                    break;
                case 1:
                    returnValue = ScaleSKPoint(new SKPoint(rand.Next(canvasWidth), canvasHeight), mapCenter, mapScale);
                    break;
                case 2:
                    returnValue = ScaleSKPoint(new SKPoint(0, rand.Next(canvasHeight)), mapCenter, mapScale);
                    break;
                case 3:
                    returnValue = ScaleSKPoint(new SKPoint(canvasWidth, rand.Next(canvasHeight)), mapCenter, mapScale);
                    break;
            }
            return returnValue;
        }
        ColoredPolygon generateStraightLineEnemy()
        {
            ColoredPolygon enemy = new ColoredPolygon(new List<SKPoint> { new SKPoint(0, 0), new SKPoint(50, 50), new SKPoint(0, 100), new SKPoint(100, 50)}, new SKPaint { Color = new SKColor(255, 100, 0)}, 0, 0, 30);
            var randomPoint = generateRandomPointOnEdgeOfMap();
            enemy.setCenter(randomPoint.X, randomPoint.Y);
            var randRotation = rand.NextDouble() * 2 * Math.PI;
            enemy.accelerateInRotation(randRotation, 10);
            enemy.setRotation(randRotation);
            return enemy;
        }
        ColoredPolygon generateTrackingEnemy()
        {
            SKPoint mapCenter = new SKPoint(canvasWidth / 2f, canvasHeight / 2f);
            ColoredPolygon enemy = new ColoredPolygon(new List<SKPoint> { new SKPoint(-50, -50), new SKPoint(0, 0), new SKPoint(-50, 50), new SKPoint(50, 0) }, new SKPaint { Color = new SKColor(255, 0, 0) }, 0, 0, 30);
            var pointOnEdge = generateRandomPointOnEdgeOfMap();
            enemy.setCenter(pointOnEdge.X, pointOnEdge.Y);
            return enemy;
        }
        ////////////////////////////////////////////////////////////////////////////////////////
        IDispatcherTimer gameLoopTimer;
        void applyCurrentFilter(SKSurface canvasSurface)
        {
            using var snapshot = canvasSurface.Snapshot();
            using var paint = new SKPaint { ColorFilter = currentColorFilter };
            canvasSurface.Canvas.Clear(SKColors.Transparent);
            canvasSurface.Canvas.DrawImage(snapshot, new SKPoint(0, 0), paint);
        }
        void drawDashProgressBar(SKCanvas canvas)
        {
            using var borderPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 5, Style = SKPaintStyle.Stroke};
            using var fillPaint = new SKPaint { Color = SKColors.White };
            canvas.DrawRect(canvasWidth * 3 / 8, canvasHeight - canvasHeight / 20, canvasWidth / 4, canvasHeight / 40, borderPaint);
            canvas.DrawRect(canvasWidth * 3 / 8, canvasHeight - canvasHeight / 20, (float)((canvasWidth / 4.0) * (1 - dashCooldownLeft / dashCooldownDuration)), canvasHeight / 40, fillPaint);
        }
        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            canvasWidth = e.Info.Width;
            canvasHeight = e.Info.Height;
            var surface = e.Surface;
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Black);
            drawDashProgressBar(canvas);
            foreach (var enemy in straightLineEnemies)
            {
                enemy.drawShinyScaled(canvas);
            }
            foreach (var enemy in followingEnemies)
            {
                enemy.drawShinyScaled(canvas);
            }
            for (int i = currentAnimations.Count - 1; i >= 0; i--)
            {
                var animation = currentAnimations[i];
                if (animation.animationFrames.Count != animation.currentFrame)
                    animation.drawCurrentFrameScaled(canvas);
                else
                {
                    animation.Dispose();
                    currentAnimations.RemoveAt(i);
                }
            }
            if (playerSquare.currentDash != null)
            {
                playerSquare.currentDash.drawDashScaled(canvas);
            }
            playerSquare.drawShinyScaled(canvas);
            applyCurrentFilter(surface);
        }
        public class CanvasAnimation
        {
            public int currentFrame = 0;
            public SKPoint centerPoint;
            public List<SKSurface> animationFrames;
            public CanvasAnimation(List<SKSurface> animationFrames, SKPoint centerPoint)
            {
                this.animationFrames = animationFrames;
                this.centerPoint = centerPoint;
            }
            public void drawCurrentFrameScaled(SKCanvas canvas)
            {
                var frame = animationFrames[currentFrame];
                using var snapshot = frame.Snapshot();
                var mapCenter = new SKPoint(canvasWidth / 2f, canvasHeight / 2f);
                SKPoint frameTopLeft = centerPoint - new SKPoint(snapshot.Width / 2, snapshot.Height / 2);
                var frameTopLeftScaled = ScaleSKPoint(frameTopLeft, mapCenter, 1 / mapScale);
                var frameBottomRight = frameTopLeft + new SKPoint(snapshot.Width, snapshot.Height);
                var frameBottomRightScaled = ScaleSKPoint(frameBottomRight, mapCenter, 1 / mapScale);
                var destinationRectangle = new SKRect(frameTopLeftScaled.X, frameTopLeftScaled.Y, frameBottomRightScaled.X, frameBottomRightScaled.Y);
                canvas.DrawImage(snapshot, destinationRectangle);
                currentFrame++;
            }
            public void Dispose()
            {
                for (int i = animationFrames.Count - 1; i <= 0; i--)
                {
                    var frame = animationFrames[i];
                    frame.Canvas.Dispose();
                    frame.Dispose();
                    animationFrames.RemoveAt(i);
                }
            }
        }
        public class Dash
        {
            public bool enemyHit = false;
            public int frameDurationLeft;
            public ColoredPolygon polygonParent;
            public List<List<SKPoint>> previousPositions = new List<List<SKPoint>>();
            public Dash(ColoredPolygon polygonParent, int frameDuration)
            {
                this.polygonParent = polygonParent;
                polygonParent.currentDash = this;
                frameDurationLeft = frameDuration;
            }
            public void updatePreviousPositions()
            {
                List<SKPoint> points = new List<SKPoint>();
                foreach (var p in polygonParent.points)
                {
                    points.Add(new SKPoint(p.X, p.Y));
                }
                previousPositions.Add(points);
            }
            public void drawDashScaled(SKCanvas canvas)
            {
                if (previousPositions.Count != 0)
                {
                    var parentColor = polygonParent.paint.Color;
                    using var paint = new SKPaint
                    {
                        Color = new SKColor((byte)((parentColor.Red + 255)/2), (byte)((parentColor.Green + 255)/2), (byte)((parentColor.Blue + 255)/2), 200),
                        StrokeWidth = (float)(6 / mapScale),
                        Style = SKPaintStyle.Stroke,
                        IsAntialias = true,
                        StrokeJoin = SKStrokeJoin.Round
                    };
                    SKPoint mapCenter = new SKPoint(canvasWidth / 2f, canvasHeight / 2f);
                    for (int i = 0; i < previousPositions.Last().Count; i++)
                    {
                        using SKPath path = new SKPath();
                        for (int j = 0; j < previousPositions.Count; j++)
                        {
                            if (j == 0)
                                path.MoveTo(ScaleSKPoint(previousPositions[j][i], mapCenter, 1 / mapScale));
                            else
                            {
                                var currentPosition = previousPositions[j];
                                var currentPoint = currentPosition[i];
                                path.LineTo(ScaleSKPoint(currentPoint, mapCenter, 1 / mapScale));
                            }
                        }
                        canvas.DrawPath(path, paint);
                    }
                }
            }
        }
        public class ColoredPolygon
        {
            public Dash? currentDash = null;
            public float maxVelocity = 10000;
            public double rotation = 0;//starts at 0 (is in radians)
            public SKPoint center;
            public float xVelocity;
            public float yVelocity;
            public List<SKPoint> points;
            public SKPaint paint;
            public SKPaint glowPaint;
            public ColoredPolygon(List<SKPoint> points, SKPaint c, int xAcceleration, int yAcceleration, double glowSize)
            {
                this.paint = c;
                this.points = points;
                glowPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = (float)glowSize,
                    Color = new SKColor(paint.Color.Red, paint.Color.Green, paint.Color.Blue, 255),
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round,
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)(glowSize / 1.5))
                };
                updateCenter();
            }
            public void updateCenter()
            {
                double centerX = 0;
                double centerY = 0;
                foreach (var point in points)
                {
                    centerX += point.X;
                    centerY += point.Y;
                }
                centerX /= points.Count;
                centerY /= points.Count;
                center = new SKPoint((float)centerX, (float)centerY);
            }
            public void drawScaled(SKCanvas canvas)
            {
                List<SKPoint> scaledPoints = new List<SKPoint>();
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    scaledPoints.Add(ScaleSKPoint(point, new SKPoint(canvasWidth / 2f, canvasHeight / 2f), 1 / mapScale));
                }
                using SKPath path = new SKPath();
                path.MoveTo(scaledPoints[0]);
                for (int i = 1; i < scaledPoints.Count; i++)
                {
                    path.LineTo(scaledPoints[i]);
                }
                path.Close();
                canvas.DrawPath(path, paint);
            }
            public void drawShinyScaled(SKCanvas canvas)
            {
                List<SKPoint> scaledPoints = new List<SKPoint>();
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    scaledPoints.Add(ScaleSKPoint(point, new SKPoint(canvasWidth / 2f, canvasHeight / 2f), 1 / mapScale));
                }
                drawScaled(canvas);
                using var path = new SKPath();
                path.MoveTo(scaledPoints[0]);
                for (int i = 1; i < scaledPoints.Count; i++)
                    path.LineTo(scaledPoints[i]);
                path.Close();
                using var scaledGlowPaint = new SKPaint 
                {
                    Style = SKPaintStyle.StrokeAndFill,
                    StrokeWidth = (float)(glowPaint.StrokeWidth / mapScale),
                    Color = glowPaint.Color,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round,
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glowPaint.StrokeWidth / 1.5f)
                };
                canvas.DrawPath(path, scaledGlowPaint);
            }
            public bool doesIntersect(ColoredPolygon other)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    SKPoint p1 = points[i];
                    SKPoint p2 = points[(i + 1) % points.Count];
                    for (int j = 0; j < other.points.Count; j++)
                    {
                        SKPoint p1Other = other.points[j];
                        SKPoint p2Other = other.points[(j + 1) % other.points.Count];
                        if (doLinesIntersect(p1, p2, p1Other, p2Other))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            public void translate(double xMovement, double yMovement)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    points[i] = new SKPoint((float)(point.X + xMovement), (float)(point.Y + yMovement));
                }
                updateCenter();
            }
            public void setCenter(double x, double y)
            {
                SKPoint difference = new SKPoint((float)x, (float)y) - center;
                for (int i = i = 0; i < points.Count; i++)
                {
                    points[i] = new SKPoint(points[i].X + difference.X, points[i].Y + difference.Y);
                }
                updateCenter();
            }
            public void activatePhysics()
            {
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = new SKPoint(points[i].X + xVelocity, points[i].Y + yVelocity);
                }
                if (currentDash == null)
                {
                    var currentAngle = Math.Atan2(yVelocity, xVelocity);
                    accelerateInRotation(currentAngle + Math.PI, 1);
                }
                updateCenter();
            }
            public void rotate(double degreeInRadians)// in radians
            {
                rotation += degreeInRadians;
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = rotateAroundPoint(points[i], center, degreeInRadians);
                }
            }
            public void setRotation(double newRotation)
            {
                rotate(newRotation - rotation);
            }
            public void accelerateInRotation(double directionAngle, double radius)
            {
                if (Math.Abs(radius) > 0.01)
                {
                    xVelocity += (float)(Math.Cos(directionAngle) * radius);
                    yVelocity += (float)(Math.Sin(directionAngle) * radius);
                    if (Math.Sqrt(xVelocity * xVelocity + yVelocity * yVelocity) > maxVelocity)
                    {
                        double currentAngle = Math.Atan2(yVelocity, xVelocity);
                        xVelocity = (float)(maxVelocity * Math.Cos(currentAngle));
                        yVelocity = (float)(maxVelocity * Math.Sin(currentAngle));
                    }
                }
            }
            public void accelerateInCurrentRotation(double radius)
            {
                accelerateInRotation(rotation, radius);
            }
            public double rotationTo(ColoredPolygon other)
            {
                return Math.Atan2(other.center.Y - center.Y, other.center.X - center.X);
            }
            public void Dispose()
            {
                paint.Dispose();
                glowPaint.Dispose();
            }
        }
        static SKPoint rotateAroundPoint(SKPoint input, SKPoint pivot, double degreesInRadians)
        {
            input -= pivot;
            double currentAngle = Math.Atan2(input.Y, input.X);
            currentAngle += degreesInRadians;
            double h = Math.Sqrt(input.X * input.X + input.Y * input.Y);
            input.X = (float)(Math.Cos(currentAngle) * h);
            input.Y = (float)(Math.Sin(currentAngle) * h);
            input += pivot;
            return input;
        }
        private async void VisualCanvas_Loaded(object sender, EventArgs e)
        {
            Debug.WriteLine("Canvas loaded!");
            gameLoopTimer = Microsoft.Maui.Controls.Application.Current.Dispatcher.CreateTimer();
            gameLoopTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps); // ~60 FPS
            gameLoopTimer.Tick += GameLoopTimer_Tick;
            gameLoopTimer.Start();
            playerSquare.setCenter(Width / 2, Height / 2);
            playerSquare.maxVelocity = 30;
        }
        async Task enemyHit(ColoredPolygon enemy)
        {
            //soundManager.Play("enemy_hit");
            gameLoopTimer.Stop();
            currentAnimations.Add(generateEnemyDeathAnimation(enemy));
            screenShake(5);
            await Task.Delay(150);
            //soundManager.Play("enemy_explodes");
            screenShake(30);
            if (followingEnemies.Contains(enemy))
                followingEnemies.Remove(enemy);
            if (straightLineEnemies.Contains(enemy))
                straightLineEnemies.Remove(enemy);
            enemy.Dispose();
            gameLoopTimer.Start();
        }
        async void gameOver()
        {
            gameLoopTimer.Stop();
            await screenShake(30);
            await Task.Delay(1000);
            await Navigation.PopAsync();
            gameLoopTimer.Stop();
            playerSquare.Dispose();
            foreach (var enemy in straightLineEnemies)
            {
                enemy.Dispose();
            }
            foreach (var enemy in followingEnemies)
            {
                enemy.Dispose();
            }
            foreach (var a in currentAnimations)
            {
                a.Dispose();
            }
            mapScale = 1;
        }
        async void playerDash()
        {
            keysEnabled = false;
            playerInvincible = true;
            playerSquare.currentDash = new Dash(playerSquare, 5);
            currentColorFilter = negativeFilter;
            float xAcceleration = 0;
            float yAcceleration = 0;
            float radius = 20;
            float distanceIfDiagonal = (float)(radius / Math.Sqrt(2));
            if ((IsKeyPressed(0x57) && IsKeyPressed(0x53)) || (IsKeyPressed(0x41) && IsKeyPressed(0x44))) // W + S or A + D
            {
                
            }
            else if (IsKeyPressed(0x57) && IsKeyPressed(0x41)) // W + A
            {
                xAcceleration -= distanceIfDiagonal;
                yAcceleration -= distanceIfDiagonal;
            }
            else if (IsKeyPressed(0x57) && IsKeyPressed(0x44)) // W + D
            {
                xAcceleration += distanceIfDiagonal;
                yAcceleration -= distanceIfDiagonal;
            }
            else if (IsKeyPressed(0x53) && IsKeyPressed(0x41)) // S + A
            {
                xAcceleration -= distanceIfDiagonal;
                yAcceleration += distanceIfDiagonal;
            }
            else if (IsKeyPressed(0x53) && IsKeyPressed(0x44)) // S + D
            {
                xAcceleration += distanceIfDiagonal;
                yAcceleration += distanceIfDiagonal;
            }
            else
            {
                if (IsKeyPressed(0x57)) // W
                    yAcceleration -= radius;
                if (IsKeyPressed(0x53)) // S
                    yAcceleration += radius;
                if (IsKeyPressed(0x41)) // A
                    xAcceleration -= radius;
                if (IsKeyPressed(0x44)) // D
                    xAcceleration += radius;
            }

            playerSquare.xVelocity = xAcceleration * 6;
            playerSquare.yVelocity = yAcceleration * 6;

            while (playerSquare.currentDash.frameDurationLeft > 0)
            {
                await Task.Delay(60);
            }

            //playerSquare.xVelocity = xAcceleration * 2;
            //playerSquare.yVelocity = yAcceleration * 2;

            currentColorFilter = defaultFilter;
            playerSquare.currentDash = null;
            playerInvincible = false;
            keysEnabled = true;
        }
        async private void GameLoopTimer_Tick(object? sender, EventArgs e)
        {
            if (dashCooldownLeft > 0)
            {
                dashCooldownLeft -= 1000.0 / fps;
                if (dashCooldownLeft < 0)
                {
                    dashCooldownLeft = 0;
                }
            }
            else
            {
                dashOnCooldown = false;
            }
            
            if (keysEnabled)
            {
                if (IsKeyPressed(0x57)) // W
                    playerSquare.accelerateInRotation(Math.PI * 1.5, 2);
                if (IsKeyPressed(0x53)) // S
                    playerSquare.accelerateInRotation(Math.PI / 2, 2);
                if (IsKeyPressed(0x41)) // A
                    playerSquare.accelerateInRotation(Math.PI, 2);
                if (IsKeyPressed(0x44)) // D
                    playerSquare.accelerateInRotation(0, 2);
                if (IsKeyPressed(0x20)) // SpaceBar
                    if (!dashOnCooldown)
                    {
                        dashOnCooldown = true;
                        playerDash();
                        dashCooldownLeft = dashCooldownDuration;
                    }
            }
            if (playerSquare.currentDash != null)
            {
                playerSquare.currentDash.frameDurationLeft--;
                playerSquare.currentDash.updatePreviousPositions();
                playerSquare.rotate(0.5);
                if (playerSquare.currentDash.enemyHit)
                {
                    dashCooldownLeft = Math.Max(dashCooldownLeft, dashCooldownDuration / 2);
                }
            }
            else
            {
                playerSquare.rotate(0.1);
            }
            playerSquare.activatePhysics();
            if (rand.Next(100) == 0)
            {
                straightLineEnemies.Add(generateStraightLineEnemy());
            }
            foreach (var enemy in straightLineEnemies)
            {
                enemy.translate(enemy.xVelocity, enemy.yVelocity);
            }
            if (rand.Next(60) == 0)
            {
                followingEnemies.Add(generateTrackingEnemy());
            }
            List<ColoredPolygon> enemiesHit = new List<ColoredPolygon>();
            if (followingEnemies.Count > 0)
            {
                for (int i = followingEnemies.Count - 1; i >= 0; i--)
                {
                    var enemy = followingEnemies[i];
                    var newRotation = enemy.rotationTo(playerSquare);
                    enemy.setRotation(newRotation);
                    enemy.accelerateInCurrentRotation(1.5);
                    enemy.activatePhysics();
                    if (playerSquare.doesIntersect(enemy))
                    {
                        if (!playerInvincible) //the player should be dead if the player isn't invincible
                            gameOver();
                        else if (playerSquare.currentDash != null)
                        {
                            playerSquare.currentDash.enemyHit = true;
                            enemiesHit.Add(enemy);
                        }
                    }
                }
            }
            if (straightLineEnemies.Count > 0)
            {
                for (int i = straightLineEnemies.Count - 1; i >= 0; i--)
                {
                    var enemy = straightLineEnemies[i];
                    if (playerSquare.doesIntersect(enemy))
                    {
                        if (!playerInvincible) //the player should be dead if the player isn't invincible
                            gameOver();
                        else if (playerSquare.currentDash != null)
                        {
                            playerSquare.currentDash.enemyHit = true;
                            enemiesHit.Add(enemy);
                        }
                    }
                }
            }
            foreach (var enemy in followingEnemies.Union(straightLineEnemies))
            {
                if (enemy.center.Y > canvasHeight * 2 || enemy.center.Y < -canvasHeight || enemy.center.X > canvasWidth * 2 || enemy.center.X < -canvasWidth)
                {
                    straightLineEnemies.Remove(enemy);
                    enemy.Dispose();
                    break;
                }
            }
            if (playerSquare.currentDash != null)
            {
                if (playerSquare.currentDash.enemyHit)
                {
                    dashCooldownLeft = Math.Min(dashCooldownLeft, dashCooldownDuration / 2);
                }
            }
            moveInsideMapBounds(playerSquare);
            visualCanvas.InvalidateSurface();
            foreach (var enemy in enemiesHit)
            {
                await enemyHit(enemy);
            }
        }
        static SKPoint ScaleSKPoint(SKPoint point, SKPoint pivot, double scalar)
        {
            return new SKPoint((float)((point.X - pivot.X) * scalar + pivot.X), (float)((point.Y - pivot.Y) * scalar + pivot.Y));
        }
        void moveInsideMapBounds(ColoredPolygon polygon)
        {
            var mapCenter = new SKPoint(canvasWidth / 2f, canvasHeight / 2f);
            if (polygon.center.X < mapCenter.X - (canvasWidth / 2f) * mapScale)
            {
                polygon.setCenter(mapCenter.X - (canvasWidth / 2f) * mapScale, polygon.center.Y);
                polygon.xVelocity = -polygon.xVelocity;
            }
            else if (polygon.center.X > mapCenter.X + (canvasWidth / 2f) * mapScale)
            {
                polygon.setCenter(mapCenter.X + (canvasWidth / 2f) * mapScale, polygon.center.Y);
                polygon.xVelocity = -polygon.xVelocity;
            }
            if (polygon.center.Y < mapCenter.Y - (canvasHeight / 2f) * mapScale)
            {
                polygon.setCenter(polygon.center.X, mapCenter.Y - (canvasHeight / 2f) * mapScale);
                polygon.yVelocity = -polygon.yVelocity;
            }
            else if (polygon.center.Y > mapCenter.Y + (canvasHeight / 2f) * mapScale)
            {
                polygon.setCenter(polygon.center.X, mapCenter.Y + (canvasHeight / 2f) * mapScale);
                polygon.yVelocity = -polygon.yVelocity;
            }
        }
        private bool IsKeyPressed(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0; //function from chatGpt
        }
        async Task screenShake(int shakeStrength)
        {
            for (int i = shakeStrength; i >= 0; i--)
            {
                double xOffset = (rand.NextDouble() - 0.5) * shakeStrength / 5;
                double yOffset = (rand.NextDouble() - 0.5) * shakeStrength / 5;
                await Task.Delay(10);
                visualCanvas.TranslationX += xOffset;
                visualCanvas.TranslationY += yOffset;
            }
            visualCanvas.TranslationX = 0;
            visualCanvas.TranslationY = 0;
        }
        private void ContentPage_Disappearing(object sender, EventArgs e)
        {
            gameLoopTimer.Stop();
            playerSquare.Dispose();
            foreach (var enemy in straightLineEnemies)
            {
                enemy.Dispose();
            }
            foreach (var enemy in followingEnemies)
            {
                enemy.Dispose();
            }
            foreach (var a in currentAnimations)
            {
                a.Dispose();
            }
            soundManager.Dispose();
        }
        static bool doLinesIntersect(SKPoint a1, SKPoint a2, SKPoint b1, SKPoint b2)
        {
            double slopeA = (a2.Y - a1.Y) / (a2.X - a1.X);
            double slopeB = (b2.Y - b1.Y) / (b2.X - b1.X);
            if (Math.Abs(slopeA - slopeB) <= double.Epsilon)
            {
                return false;
            }
            double constantA = a1.Y - slopeA * a1.X;
            double constantB = b1.Y - slopeB * b1.X;
            double intersectionX = (constantB - constantA) / (slopeA - slopeB);
            if (intersectionX <= Math.Max(a1.X, a2.X) && intersectionX >= Math.Min(a1.X, a2.X) && intersectionX <= Math.Max(b1.X, b2.X) && intersectionX >= Math.Min(b1.X, b2.X))
            {
                return true;
            }
            return false;
        }



    }
}
