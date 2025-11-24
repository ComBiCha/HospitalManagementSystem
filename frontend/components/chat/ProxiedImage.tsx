
'use client';

import React, { useEffect, useState } from 'react';
import Image from 'next/image';
import { api } from '@/lib/api';
import { Loader2 } from 'lucide-react';

interface ProxiedImageProps {
  src: string;
  alt: string;
  width: number;
  height: number;
  className: string;
}

const ProxiedImage: React.FC<ProxiedImageProps> = ({ src, alt, ...props }) => {
  const [objectUrl, setObjectUrl] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    // Reset state when src changes
    setObjectUrl(null);
    setError(null);
    
    if (src && !src.startsWith('blob:')) {
      setIsLoading(true);
      api.get(src, { responseType: 'blob' })
        .then(response => {
          if (isMounted) {
            const url = URL.createObjectURL(response.data);
            setObjectUrl(url);
          }
        })
        .catch(err => {
          console.error('Error fetching proxied image:', err);
          if (isMounted) {
            setError('Could not load image.');
          }
        })
        .finally(() => {
          if (isMounted) {
            setIsLoading(false);
          }
        });
    } else if (src) {
      setObjectUrl(src);
    }

    return () => {
      isMounted = false;
      if (objectUrl && objectUrl.startsWith('blob:')) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [src]);

  if (isLoading) {
    return (
        <div 
            className="flex items-center justify-center bg-gray-200 rounded-lg" 
            style={{ width: props.width, height: props.height }}
        >
            <Loader2 className="animate-spin text-gray-500" />
        </div>
    );
  }

  if (error || !objectUrl) {
    return (
        <div 
            className="flex items-center justify-center bg-gray-200 rounded-lg" 
            style={{ width: props.width, height: props.height }}
            title={error || 'Image not available'}
        >
            <span className="text-xs text-gray-500">Error</span>
        </div>
    );
  }

  // Using a standard <img> tag here because next/image optimization is
  // not applicable for blob URLs.
  // eslint-disable-next-line @next/next/no-img-element
  return <img src={objectUrl} alt={alt} {...props} />;
};

export default ProxiedImage;
