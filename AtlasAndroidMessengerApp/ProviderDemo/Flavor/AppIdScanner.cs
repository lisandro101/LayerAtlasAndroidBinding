using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Gms.Common.Images;
using Android.Gms.Vision;
using Android.Gms.Vision.Barcodes;
using Android.Graphics;
using Android.Net;
using Android.Support.V4.App;
using Android.Views;
using Java.IO;
using Java.Util;
using Java.Util.Concurrent;
using System.Collections.Generic;

namespace Com.Layer.Messenger.Flavor
{
    public class AppIdScanner : ViewGroup
    {
        private SurfaceView mSurfaceView;
        private BarcodeDetector mBarcodeDetector;
        private Detector.IProcessor mAppIdProcessor;
        private CameraSource.Builder mCameraBuilder;

        private AppIdCallback mAppIdCallback;
        private bool mStartRequested;
        private bool mSurfaceAvailable;
        private CameraSource mCameraSource;

        public AppIdScanner(Context context)
            : base(context)
        {
            Init();
        }

        public AppIdScanner(Context context, Android.Util.IAttributeSet attrs)
            : this(context, attrs, 0)
        {
        }

        public AppIdScanner(Context context, Android.Util.IAttributeSet attrs, int defStyleAttr)
            : base(context, attrs, defStyleAttr)
        {
            Init();
        }

        private void Init() {
            mStartRequested = false;
            mSurfaceAvailable = false;

            mAppIdProcessor = new BarcodeDetectorProcessor(this);

            mBarcodeDetector = new BarcodeDetector.Builder(Context)
                    .SetBarcodeFormats(BarcodeFormat.QrCode)
                    .Build();
            mBarcodeDetector.SetProcessor(mAppIdProcessor);

            mCameraBuilder = new CameraSource.Builder(Context, mBarcodeDetector)
                    .SetFacing(CameraFacing.Back)
                    .SetAutoFocusEnabled(true)
                    .SetRequestedFps(30.0f);

            mSurfaceView = new SurfaceView(Context);
            mSurfaceView.Holder.AddCallback(new SurfaceCallback(this));
            AddView(mSurfaceView);
        }

        public AppIdScanner SetAppIdCallback(AppIdCallback appIdCallback) {
            mAppIdCallback = appIdCallback;
            return this;
        }

        public void Start() {
            mStartRequested = true;
            StartIfReady();
        }

        public void Stop() {
            if (mCameraSource != null) mCameraSource.Stop();
            mSurfaceView.Visibility = ViewStates.Gone;
        }

        public void Release() {
            if (mCameraSource != null) mCameraSource.Release();
            mBarcodeDetector.Release();
            mAppIdProcessor.Release();
        }

        private void StartIfReady() {
            if (!mStartRequested || !mSurfaceAvailable || mCameraSource == null) return;
            if (ActivityCompat.CheckSelfPermission(Context, Android.Manifest.Permission.Camera) != Permission.Granted) {
                if (Util.Log.IsLoggable(Util.Log.ERROR)) {
                    Util.Log.e("Required permission `" + Android.Manifest.Permission.Camera + "` not granted.");
                }
                return;
            }
            try {
                mCameraSource.Start(mSurfaceView.Holder);
                mStartRequested = false;
            } catch (IOException e) {
                if (Util.Log.IsLoggable(Util.Log.ERROR)) {
                    Util.Log.e(e.Message, e);
                }
            }
        }

        private class SurfaceCallback : Java.Lang.Object, ISurfaceHolderCallback
        {
            private AppIdScanner _appIdScanner;

            public SurfaceCallback(AppIdScanner appIdScanner)
            {
                _appIdScanner = appIdScanner;
            }

            public void SurfaceCreated(ISurfaceHolder surface) {
                _appIdScanner.mSurfaceAvailable = true;
                _appIdScanner.StartIfReady();
            }

            public void SurfaceDestroyed(ISurfaceHolder surface) {
                _appIdScanner.mSurfaceAvailable = false;
            }

            public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height) {
            }
        }

        private class BarcodeDetectorProcessor : Java.Lang.Object, Detector.IProcessor
        {
            private AppIdScanner _appIdScanner;

            public BarcodeDetectorProcessor(AppIdScanner appIdScanner)
            {
                _appIdScanner = appIdScanner;
            }

            public void Release()
            {
            }

            public void ReceiveDetections(Detector.Detections detections)
            {
                Android.Util.SparseArray barcodes = detections.DetectedItems;
                for (int i = 0; i < barcodes.Size(); i++)
                {
                    Barcode barcode = barcodes.ValueAt(i) as Barcode;
                    string value = barcode.DisplayValue;
                    try
                    {
                        Uri appId = Uri.Parse(value);
                        if (!appId.Scheme.Equals("layer"))
                        {
                            throw new Java.Lang.IllegalArgumentException("URI is not an App ID");
                        }
                        if (!appId.Authority.Equals(""))
                        {
                            throw new Java.Lang.IllegalArgumentException("URI is not an App ID");
                        }
                        IList<string> segments = appId.PathSegments;
                        if (segments.Count != 3)
                        {
                            throw new Java.Lang.IllegalArgumentException("URI is not an App ID");
                        }
                        if (!segments[0].Equals("apps"))
                        {
                            throw new Java.Lang.IllegalArgumentException("URI is not an App ID");
                        }
                        if (!segments[1].Equals("staging") && !segments[1].Equals("production"))
                        {
                            throw new Java.Lang.IllegalArgumentException("URI is not an App ID");
                        }
                        UUID uuid = UUID.FromString(segments[2]);
                        if (Util.Log.IsLoggable(Util.Log.VERBOSE))
                        {
                            Util.Log.v("Captured Layer App ID: " + appId + ", UUID: " + uuid);
                        }
                        if (_appIdScanner.mAppIdCallback == null) return;
                        _appIdScanner.mAppIdCallback.OnLayerAppIdScanned(_appIdScanner, appId.ToString());
                    }
                    catch (System.Exception e)
                    {
                        // Not this barcode...                        
                        if (Util.Log.IsLoggable(Util.Log.ERROR))
                        {
                            Util.Log.e("Barcode does not contain an App ID URI: " + value, e);
                        }
                    }
                }
            }
        }

        protected override void OnLayout(bool isChange, int left, int top, int right, int bottom) {
            if (!isChange) return;
            bool isPortrait = Context.Resources.Configuration.Orientation == Orientation.Portrait;
            int parentWidth = right - left;
            int parentHeight = bottom - top;
            mSurfaceView.Layout(0, 0, 1, 1);

            int requestWidth = isPortrait ? parentHeight : parentWidth;
            int requestHeight = isPortrait ? parentWidth : parentHeight;

            // Request camera preview
            if (mCameraSource != null) {
                mCameraSource.Stop();
                mCameraSource.Release();
            }
            if (Util.Log.IsLoggable(Util.Log.VERBOSE)) {
                Util.Log.v("Requesting camera preview: " + requestWidth + "x" + requestHeight);
            }
            mCameraSource = mCameraBuilder.SetRequestedPreviewSize(requestWidth, requestHeight).Build();
            StartIfReady();

            Post(() =>
            {
                double parentWidth_ = Width;
                double parentHeight_ = Height;

                Size previewSize = mCameraSource.PreviewSize;
                while (previewSize == null)
                {
                    previewSize = mCameraSource.PreviewSize;
                    try
                    {
                        TimeUnit.Milliseconds.Sleep(15);
                    }
                    catch (Java.Lang.InterruptedException)
                    {
                        // OK
                    }
                }
                if (Util.Log.IsLoggable(Util.Log.VERBOSE))
                {
                    Util.Log.v("Actual camera preview is: " + previewSize.Width + "x" + previewSize.Height);
                }

                bool isPortrait_ = Context.Resources.Configuration.Orientation == Orientation.Portrait;
                double previewWidth = isPortrait_ ? previewSize.Height : previewSize.Width;
                double previewHeight = isPortrait_ ? previewSize.Width : previewSize.Height;

                double widthRatio = previewWidth / parentWidth_;
                double heightRatio = previewHeight / parentHeight_;
                double surfaceWidth;
                double surfaceHeight;
                if (heightRatio < widthRatio)
                {
                    surfaceWidth = parentHeight_ * previewWidth / previewHeight;
                    surfaceHeight = parentHeight_;
                }
                else {
                    surfaceWidth = parentWidth_;
                    surfaceHeight = parentWidth_ * previewHeight / previewWidth;
                }

                double centerLeft = (parentWidth_ - surfaceWidth) / 2.0;
                double centerTop = (parentHeight_ - surfaceHeight) / 2.0;
                mSurfaceView.Layout((int) System.Math.Round(centerLeft), (int) System.Math.Round(centerTop), (int) System.Math.Round(surfaceWidth + centerLeft), (int) System.Math.Round(surfaceHeight + centerTop));
                if (Util.Log.IsLoggable(Util.Log.VERBOSE))
                {
                    Util.Log.v("Resized preview layout to: " + (isPortrait_ ? mSurfaceView.Height : mSurfaceView.Width) + "x" + (isPortrait_ ? mSurfaceView.Width : mSurfaceView.Height));
                }
            });
        }

        public interface AppIdCallback {
            void OnLayerAppIdScanned(AppIdScanner scanner, string layerAppId);
        }
    }
}